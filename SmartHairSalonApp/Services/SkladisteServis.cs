using SmartHairSalonApp.Controllers;
using SmartHairSalonApp.Models;

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

        /// <summary>
        /// Vraca nazive stavki iz korpe kojih ima vise nego na stanju
        /// (ili ciji proizvod vise ne postoji u bazi).
        /// </summary>
        public static List<string> NedostajuciProizvodi(
            List<StavkaKorpeViewModel> korpa, List<Proizvod> proizvodi)
        {
            var nedostaju = new List<string>();
            foreach (var stavka in korpa)
            {
                var proizvod = proizvodi.FirstOrDefault(p => p.Id == stavka.ProizvodId);
                if (proizvod == null || stavka.Kolicina > proizvod.Kolicina)
                    nedostaju.Add(stavka.Naziv);
            }
            return nedostaju;
        }
    }
}