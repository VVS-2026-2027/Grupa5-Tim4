using SmartHairSalonApp.Models;
using SmartHairSalonApp.Services;

namespace SmartHairSalonApp.Tests.TDD
{
    // TDD - Funkcionalnost 2, ciklus 3: provjera formata i vremena termina rezervacije.
    // Format termina je isti kao primjer na formi za rezervaciju: "15.06.2026 u 14:00".
    [TestClass]
    public class TerminServisTests
    {
        // Fiksno "sada", da testovi ne zavise od trenutnog vremena racunara.
        private static readonly DateTime Sada = new DateTime(2026, 11, 18, 12, 0, 0);

        // ---------- Parsiraj ----------

        [TestMethod]
        public void Parsiraj_IspravanTermin_VracaDatumIVrijeme()
        {
            var rezultat = TerminServis.Parsiraj("15.06.2026 u 14:00");

            Assert.AreEqual(new DateTime(2026, 6, 15, 14, 0, 0), rezultat);
        }

        [TestMethod]
        public void Parsiraj_RazmaciNaPocetkuIKraju_IgnoriseRazmake()
        {
            var rezultat = TerminServis.Parsiraj("  15.06.2026 u 14:00  ");

            Assert.AreEqual(new DateTime(2026, 6, 15, 14, 0, 0), rezultat);
        }

        [TestMethod]
        public void Parsiraj_TerminJeNull_VracaNull()
        {
            var rezultat = TerminServis.Parsiraj(null);

            Assert.IsNull(rezultat);
        }

        // Data driven (DataRow): svi neispravni oblici termina vracaju null
        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("sutra u 10")]
        [DataRow("15.06.2026 14:00")]
        [DataRow("31.02.2026 u 10:00")]
        [DataRow("15.06.2026 u 25:00")]
        public void Parsiraj_NeispravanTermin_VracaNull(string termin)
        {
            var rezultat = TerminServis.Parsiraj(termin);

            Assert.IsNull(rezultat);
        }

        // ---------- JeUBuducnosti ----------

        [TestMethod]
        public void JeUBuducnosti_TerminSatKasnije_VracaTrue()
        {
            var rezultat = TerminServis.JeUBuducnosti(Sada.AddHours(1), Sada);

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void JeUBuducnosti_IstiTrenutak_VracaFalse()
        {
            var rezultat = TerminServis.JeUBuducnosti(Sada, Sada);

            Assert.IsFalse(rezultat);
        }

        [TestMethod]
        public void JeUBuducnosti_TerminJuce_VracaFalse()
        {
            var rezultat = TerminServis.JeUBuducnosti(Sada.AddDays(-1), Sada);

            Assert.IsFalse(rezultat);
        }

        // ---------- JeSlobodan (TDD - Funkcionalnost 2, ciklus 4) ----------

        private static readonly DateTime Termin = new DateTime(2030, 6, 15, 14, 0, 0);
        private const string TerminTekst = "15.06.2030 u 14:00";

        private static Rezervacija NovaRezervacija(int uslugaId, string? termin, StatusRezervacije status)
        {
            return new Rezervacija { UslugaId = uslugaId, ZeljeniTermin = termin, StatusRezervacije = status, KorisnikId = "k1" };
        }

        [TestMethod]
        public void JeSlobodan_NemaRezervacija_VracaTrue()
        {
            var rezultat = TerminServis.JeSlobodan(new List<Rezervacija>(), 1, Termin);

            Assert.IsTrue(rezultat);
        }

        // Data driven (DataRow): ista usluga u isto vrijeme zauzima termin samo ako rezervacija nije odbijena
        [TestMethod]
        [DataRow(StatusRezervacije.UObradi, false)]
        [DataRow(StatusRezervacije.Potvrdjena, false)]
        [DataRow(StatusRezervacije.Odbijena, true)]
        public void JeSlobodan_IstaUslugaIstoVrijeme_ZavisiOdStatusa(StatusRezervacije status, bool ocekivano)
        {
            var rezervacije = new List<Rezervacija> { NovaRezervacija(1, TerminTekst, status) };

            var rezultat = TerminServis.JeSlobodan(rezervacije, 1, Termin);

            Assert.AreEqual(ocekivano, rezultat);
        }

        [TestMethod]
        public void JeSlobodan_IstaUslugaDrugoVrijeme_VracaTrue()
        {
            var rezervacije = new List<Rezervacija> { NovaRezervacija(1, "15.06.2030 u 15:00", StatusRezervacije.Potvrdjena) };

            var rezultat = TerminServis.JeSlobodan(rezervacije, 1, Termin);

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void JeSlobodan_DrugaUslugaIstoVrijeme_VracaTrue()
        {
            var rezervacije = new List<Rezervacija> { NovaRezervacija(2, TerminTekst, StatusRezervacije.Potvrdjena) };

            var rezultat = TerminServis.JeSlobodan(rezervacije, 1, Termin);

            Assert.IsTrue(rezultat);
        }

        // Stare rezervacije su imale slobodan tekst ("20.11. u 10:00", null), pa se preskacu
        [TestMethod]
        [DataRow("20.11. u 10:00")]
        [DataRow("sutra")]
        [DataRow((string?)null)]
        public void JeSlobodan_StaraRezervacijaNeispravnogFormata_SePreskace(string? stariTermin)
        {
            var rezervacije = new List<Rezervacija> { NovaRezervacija(1, stariTermin, StatusRezervacije.Potvrdjena) };

            var rezultat = TerminServis.JeSlobodan(rezervacije, 1, Termin);

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void JeSlobodan_ViseRezervacijaJednaZauzimaTermin_VracaFalse()
        {
            var rezervacije = new List<Rezervacija>
            {
                NovaRezervacija(1, "15.06.2030 u 10:00", StatusRezervacije.Potvrdjena),
                NovaRezervacija(2, TerminTekst, StatusRezervacije.Potvrdjena),
                NovaRezervacija(1, TerminTekst, StatusRezervacije.UObradi)
            };

            var rezultat = TerminServis.JeSlobodan(rezervacije, 1, Termin);

            Assert.IsFalse(rezultat);
        }
    }
}
