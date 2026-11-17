using System.Globalization;

namespace SmartHairSalonApp.Services
{
    public static class TerminServis
    {
        public static DateTime? Parsiraj(string termin)
        {
            if (termin == null)
            {
                return null;
            }

            if (DateTime.TryParseExact(termin.Trim(), "dd.MM.yyyy 'u' HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rezultat))
            {
                return rezultat;
            }

            return null;
        }

        public static bool JeUBuducnosti(DateTime termin, DateTime sada)
        {
            if (termin > sada)
            {
                return true;
            }

            return false;
        }
    }
}
