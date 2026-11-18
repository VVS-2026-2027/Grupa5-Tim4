using System.Globalization;
using SmartHairSalonApp.Models;

namespace SmartHairSalonApp.Services
{
    /// <summary>
    /// Provjera termina rezervacije: format termina i da li je termin u buducnosti.
    /// Primjer ispravnog termina: "15.06.2026 u 14:00".
    /// </summary>
    public static class TerminServis
    {
        public const string FormatTermina = "dd.MM.yyyy 'u' HH:mm";

        /// <summary>Vraca datum i vrijeme termina, ili null ako termin nije u ispravnom formatu.</summary>
        public static DateTime? Parsiraj(string termin)
        {
            if (string.IsNullOrWhiteSpace(termin))
            {
                return null;
            }

            bool ispravan = DateTime.TryParseExact(termin.Trim(), FormatTermina,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime vrijeme);

            return ispravan ? vrijeme : null;
        }

        /// <summary>Termin je u buducnosti samo ako je poslije trenutka "sada".</summary>
        public static bool JeUBuducnosti(DateTime termin, DateTime sada) => termin > sada;

        /// <summary>Termin je slobodan ako za istu uslugu u isto vrijeme nema rezervacije koja je u obradi ili potvrdjena.</summary>
        public static bool JeSlobodan(List<Rezervacija> rezervacije, int uslugaId, DateTime termin)
        {
            foreach (var rezervacija in rezervacije)
            {
                if (rezervacija.UslugaId == uslugaId
                    && Parsiraj(rezervacija.ZeljeniTermin) == termin
                    && (rezervacija.StatusRezervacije == StatusRezervacije.UObradi
                        || rezervacija.StatusRezervacije == StatusRezervacije.Potvrdjena))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
