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
    }
}
