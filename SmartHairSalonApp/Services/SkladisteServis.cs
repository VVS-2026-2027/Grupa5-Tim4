namespace SmartHairSalonApp.Services
{
    public static class SkladisteServis
    {
        public static bool MozeSeDodati(int naStanju, int vecUKorpi, int kolicina)
        {
            if (kolicina <= 0)
            {
                throw new ArgumentOutOfRangeException("kolicina");
            }

            if (vecUKorpi + kolicina > naStanju)
            {
                return false;
            }

            return true;
        }
    }
}
