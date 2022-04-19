using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Schema;
using System.Xml.Serialization;
using KS.Fiks.Arkiv.Models.V1.Arkivering.Arkivmelding;
using KS.Fiks.Arkiv.Models.V1.Arkivering.Arkivmelding.Oppdatering;
using KS.Fiks.Arkiv.Models.V1.Meldingstyper;
using ks.fiks.io.arkivsystem.sample.Generators;
using ks.fiks.io.arkivsystem.sample.Models;
using KS.Fiks.IO.Client.Models;
using KS.Fiks.IO.Client.Models.Feilmelding;
using Serilog;

namespace ks.fiks.io.arkivsystem.sample.Handlers
{
    public class ArkivmeldingOppdaterHandler : BaseHandler
    {
        private static readonly ILogger Log = Serilog.Log.ForContext(MethodBase.GetCurrentMethod()?.DeclaringType);

        public static List<Melding> HandleMelding(MottattMeldingArgs mottatt)
        {
            var arkivmeldingXmlSchemaSet = new XmlSchemaSet();
            arkivmeldingXmlSchemaSet.Add("http://www.arkivverket.no/standarder/noark5/arkivmeldingoppdatering/v2",
                Path.Combine("Schema", "arkivmeldingOppdatering.xsd"));
            arkivmeldingXmlSchemaSet.Add("http://www.arkivverket.no/standarder/noark5/arkivmelding/v2",
                Path.Combine("Schema", "arkivmelding.xsd"));
            arkivmeldingXmlSchemaSet.Add("http://www.arkivverket.no/standarder/noark5/metadatakatalog/v2",
                Path.Combine("Schema", "metadatakatalog.xsd"));

            var meldinger = new List<Melding>();
            ArkivmeldingOppdatering arkivmeldingOppdatering = new ArkivmeldingOppdatering();
            if (mottatt.Melding.HasPayload)
            {
                arkivmeldingOppdatering = GetPayload(mottatt, arkivmeldingXmlSchemaSet,
                    out var xmlValidationErrorOccured, out var validationResult);

                if (xmlValidationErrorOccured) // Ugyldig forespørsel
                {
                    meldinger.Add(new Melding
                    {
                        ResultatMelding = FeilmeldingGenerator.CreateUgyldigforespoerselMelding(validationResult),
                        FileName = "payload.json",
                        MeldingsType = FeilmeldingMeldingTypeV1.Ugyldigforespørsel,
                    });
                    return meldinger;
                }
            }
            else
            {
                meldinger.Add(new Melding
                {
                    ResultatMelding = FeilmeldingGenerator.CreateUgyldigforespoerselMelding("ArkivmeldingOppdatering meldingen mangler innhold"),
                    FileName = "payload.json",
                    MeldingsType = FeilmeldingMeldingTypeV1.Ugyldigforespørsel,
                });
                return meldinger;
            }

            //TODO Hent arkivmelding i "cache" hvis det er en testSessionId i headere og oppdater den meldingen
            if (mottatt.Melding.Headere.TryGetValue(ArkivSimulator.TestSessionIdHeader, out var testSessionId))
            {
                Arkivmelding arkivmelding;
                ArkivSimulator._arkivmeldingCache.TryGetValue(testSessionId, out arkivmelding);
                
                // Journalpost oppdatering?
                if (arkivmeldingOppdatering.RegistreringOppdateringer.Count > 0)
                {
                     
                    foreach (var registreringOppdatering in arkivmeldingOppdatering.RegistreringOppdateringer)
                    {
                        // Tittel som skal oppdateres?
                        if (!string.IsNullOrEmpty(registreringOppdatering.Tittel))
                        {
                            foreach (var registrering in arkivmelding.Registrering)
                            {
                                // referanseEksternNoekkel er nøkkel
                                if (registreringOppdatering.ReferanseEksternNoekkel != null)
                                {
                                    if (registrering.ReferanseEksternNoekkel.Fagsystem ==
                                        registreringOppdatering.ReferanseEksternNoekkel.Fagsystem &&
                                        registrering.ReferanseEksternNoekkel.Noekkel ==
                                        registreringOppdatering.ReferanseEksternNoekkel.Noekkel)
                                    {
                                        registrering.Tittel = registreringOppdatering.Tittel;
                                    }
                                } else if (registreringOppdatering.SystemID != null) // SystemID er nøkkel
                                {
                                    if (registrering.SystemID == registreringOppdatering.SystemID)
                                    {
                                        registrering.Tittel = registreringOppdatering.Tittel;
                                    }
                                }
                                else // ID mangler og vi sender ugyldigforespoersel
                                {
                                    meldinger.Add(new Melding
                                    {
                                        ResultatMelding = FeilmeldingGenerator.CreateUgyldigforespoerselMelding("Mangler id for registrering"),
                                        FileName = "payload.json",
                                        MeldingsType = FeilmeldingMeldingTypeV1.Ugyldigforespørsel,
                                    });
                                    return meldinger;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                meldinger.Add(new Melding
                {
                    ResultatMelding = FeilmeldingGenerator.CreateUgyldigforespoerselMelding("ArkivmeldingOppdatering ikke gyldig. Kunne ikke finne noe registrert i arkivet med gitt id"),
                    FileName = "payload.json",
                    MeldingsType = FeilmeldingMeldingTypeV1.Ugyldigforespørsel,
                });
                return meldinger;
            }
            
            // Mottatt
            meldinger.Add(new Melding
            {
                MeldingsType = FiksArkivV1Meldingtype.ArkivmeldingMottatt,
            });
            
            // Kvittering
            meldinger.Add(new Melding
            {
                MeldingsType = FiksArkivV1Meldingtype.ArkivmeldingOppdaterKvittering,
            });
            return meldinger;
        }
        
        public static ArkivmeldingOppdatering GetPayload(MottattMeldingArgs mottatt, XmlSchemaSet xmlSchemaSet,
            out bool xmlValidationErrorOccured, out List<List<string>> validationResult)
        {
            if (mottatt.Melding.HasPayload)
            {
                var text = GetPayloadAsString(mottatt, xmlSchemaSet, out xmlValidationErrorOccured,
                    out validationResult);
                Log.Information("Parsing arkivmeldingOppdatering: {Xml}", text);
                if (string.IsNullOrEmpty(text))
                {
                    Log.Error("Tom arkivmeldingOppdatering? Xml: {Xml}", text);
                }

                using var textReader = (TextReader)new StringReader(text);
                return(ArkivmeldingOppdatering) new XmlSerializer(typeof(ArkivmeldingOppdatering)).Deserialize(textReader);
            }

            xmlValidationErrorOccured = false;
            validationResult = null;
            return null;
        }
    }
}