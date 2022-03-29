using System.IO;
using System.Xml.Schema;
using KS.Fiks.IO.Arkiv.Client.Models;
using KS.Fiks.IO.Arkiv.Client.Models.Arkivering.Arkivmelding;
using KS.Fiks.IO.Arkiv.Client.Models.Innsyn.Hent;
using KS.Fiks.IO.Arkiv.Client.Models.Innsyn.Hent.Journalpost;
using ks.fiks.io.arkivsystem.sample.Handlers;
using ks.fiks.io.arkivsystem.sample.Models;
using KS.Fiks.IO.Client.Models;
using KS.Fiks.IO.Client.Models.Feilmelding;
using EksternNoekkel = KS.Fiks.IO.Arkiv.Client.Models.Arkivering.Arkivmelding.EksternNoekkel;

namespace ks.fiks.io.arkivsystem.sample.Generators
{
    public class JournalpostHentGenerator
    {
        public static JournalpostHentResultat Create(JournalpostHent journalpostHent)
        {
            var journalpost = ArkivmeldingGenerator.CreateJournalpost();
            return Create(journalpostHent, journalpost);
        }
        
        public static JournalpostHentResultat Create(JournalpostHent journalpostHent, Journalpost journalpost)
        {
            if (journalpostHent.ReferanseEksternNoekkel != null)
            {
                journalpost.ReferanseEksternNoekkel = new EksternNoekkel()
                {   
                    Fagsystem = journalpostHent.ReferanseEksternNoekkel.Fagsystem,
                    Noekkel = journalpostHent.ReferanseEksternNoekkel.Noekkel
                };
            } else if (journalpostHent.SystemID != null)
            {
                journalpost.SystemID = journalpostHent.SystemID;    
            }

            return new JournalpostHentResultat()
            {
                Journalpost = journalpost
            };
        }
    }
}