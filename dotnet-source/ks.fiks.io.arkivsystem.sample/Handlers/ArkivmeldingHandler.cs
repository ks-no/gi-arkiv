using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Schema;
using System.Xml.Serialization;
using KS.Fiks.Arkiv.Models.V1.Arkivering.Arkivmelding;
using KS.Fiks.Arkiv.Models.V1.Meldingstyper;
using ks.fiks.io.arkivsystem.sample.Generators;
using ks.fiks.io.arkivsystem.sample.Models;
using ks.fiks.io.arkivsystem.sample.Storage;
using KS.Fiks.IO.Client.Models;
using Serilog;

namespace ks.fiks.io.arkivsystem.sample.Handlers
{
    public class ArkivmeldingHandler : BaseHandler, IMeldingHandler
    {
        private static readonly ILogger Log = Serilog.Log.ForContext(MethodBase.GetCurrentMethod()?.DeclaringType);

        public ArkivmeldingHandler(IArkivmeldingCache arkivmeldingCache) : base(arkivmeldingCache)
        {
        }
        
        private Arkivmelding GetPayload(MottattMeldingArgs mottatt, XmlSchemaSet xmlSchemaSet,
            out bool xmlValidationErrorOccured, out List<List<string>> validationResult)
        {
            if (mottatt.Melding.HasPayload)
            {
                var text = GetPayloadAsString(mottatt, xmlSchemaSet, out xmlValidationErrorOccured,
                    out validationResult);
                Log.Information("Parsing arkivmelding: {Xml}", text);
                if (string.IsNullOrEmpty(text))
                {
                    Log.Error("Tom arkivmelding? Xml: {Xml}", text);
                }

                using var textReader = (TextReader)new StringReader(text);
                return(Arkivmelding) new XmlSerializer(typeof(Arkivmelding)).Deserialize(textReader);
            }

            xmlValidationErrorOccured = false;
            validationResult = null;
            return null;
        }
        
        public List<Melding> HandleMelding(MottattMeldingArgs mottatt)
        {
            var meldinger = new List<Melding>();
            
            Arkivmelding arkivmelding;
            if (mottatt.Melding.HasPayload)
            {
                arkivmelding = GetPayload(mottatt, XmlSchemaSet,
                    out var xmlValidationErrorOccured, out var validationResult);

                if (xmlValidationErrorOccured) // Ugyldig forespørsel
                {
                    Log.Information($"Xml validering feilet {validationResult}");
                    meldinger.Add(new Melding
                    {
                        ResultatMelding = FeilmeldingGenerator.CreateUgyldigforespoerselMelding(validationResult),
                        FileName = "feilmelding.xml",
                        MeldingsType = FiksArkivMeldingtype.Ugyldigforespørsel,
                    });
                    return meldinger;
                }
            }
            else
            {
                meldinger.Add(new Melding
                {
                    ResultatMelding =
                        FeilmeldingGenerator.CreateUgyldigforespoerselMelding("Arkivmelding meldingen mangler innhold"),
                    FileName = "feilmelding.xml",
                    MeldingsType = FiksArkivMeldingtype.Ugyldigforespørsel,
                });
                return meldinger;
            }

            SetMissingSystemID(arkivmelding);
            var kvittering = ArkivmeldingKvitteringGenerator.CreateArkivmeldingKvittering(arkivmelding);
            
            // Lagre arkivmelding i "cache" hvis det er en testSessionId i headere
            if (mottatt.Melding.Headere.TryGetValue(ArkivSimulator.TestSessionIdHeader, out var testSessionId))
            {
                if (_arkivmeldingCache.HasArkivmeldinger(testSessionId))
                {
                    var found = false;
                    var lagretArkvivmeldinger = _arkivmeldingCache.GetAll(testSessionId);
                    foreach (var lagretArkivmelding in lagretArkvivmeldinger)
                    {
                        // Registrering som skal lagres?
                        if (arkivmelding.Registrering.Count >= 0)
                        {
                            foreach (var registrering in arkivmelding.Registrering)
                            {
                                if (registrering.ReferanseForelderMappe != null)
                                {
                                    foreach (var lagretMappe in lagretArkivmelding.Mappe)
                                    {
                                        // 
                                        if (registrering.ReferanseForelderMappe.SystemID != null &&
                                            lagretMappe.SystemID.Value ==
                                            registrering.ReferanseForelderMappe.SystemID.Value)
                                        {
                                            lagretMappe.Registrering.Add(registrering);
                                            found = true;
                                        }
                                        else if (registrering.ReferanseForelderMappe.ReferanseEksternNoekkel != null &&
                                                 registrering.ReferanseForelderMappe.ReferanseEksternNoekkel
                                                     .Fagsystem == lagretMappe.ReferanseEksternNoekkel.Fagsystem &&
                                                 registrering.ReferanseForelderMappe.ReferanseEksternNoekkel.Noekkel ==
                                                 lagretMappe.ReferanseEksternNoekkel.Noekkel)
                                        {
                                            lagretMappe.Registrering.Add(registrering);
                                            found = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!found)
                    {
                        _arkivmeldingCache.Add(testSessionId, arkivmelding);
                    }
                }
                else
                {
                    _arkivmeldingCache.Add(testSessionId, arkivmelding);
                }
            }
            
            // Det skal sendes også en mottatt melding
            meldinger.Add(new Melding
            {
                MeldingsType = FiksArkivMeldingtype.ArkivmeldingMottatt
            });
            
            meldinger.Add(new Melding
            {
                ResultatMelding = kvittering,
                FileName = "arkivmelding-kvittering.xml",
                MeldingsType = FiksArkivMeldingtype.ArkivmeldingKvittering
            });
            
            return meldinger;
        }
    }
}