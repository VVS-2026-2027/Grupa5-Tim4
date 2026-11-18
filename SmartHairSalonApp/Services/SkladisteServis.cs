namespace SmartHairSalonApp.Services
{
    /// <summary>
    /// Provjere stanja proizvoda na skladistu (zalihe) za korpu i narudzbu.
    /// </summary>
    public static class SkladisteServis
    {
        /// <summary>
        /// Da li se u korpu moze dodati jos "kolicina" komada,
        /// a da ukupno u korpi ne bude vise nego sto ima na stanju.
        /// </summary>
        public static bool MozeSeDodati(int naStanju, int vecUKorpi, int kolicina)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kolicina);

            return vecUKorpi + kolicina <= naStanju;
        }
    }
}
