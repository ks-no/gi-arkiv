using System;

namespace ks.fiks.io.arkivintegrasjon.client.Melding
{
    public class Ugyldigforespørsel
    {
        public string ErrorId { get; set; }
        public string Feilmelding { get; set; }
        public Guid SvarPåMeldingId { get; set; }
    }
}