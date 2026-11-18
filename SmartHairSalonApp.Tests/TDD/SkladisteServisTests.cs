using SmartHairSalonApp.Services;

namespace SmartHairSalonApp.Tests.TDD
{
    // TDD - Funkcionalnost 1, ciklus 1: u korpi ne smije biti vise komada nego sto ima na stanju.
    [TestClass]
    public class SkladisteServisTests
    {
        // Data driven (DataRow): ima dovoljno na stanju
        [TestMethod]
        [DataRow(5, 0, 1)]
        [DataRow(5, 4, 1)]
        [DataRow(10, 3, 7)]
        public void MozeSeDodati_DovoljnoNaStanju_VracaTrue(int naStanju, int vecUKorpi, int kolicina)
        {
            var rezultat = SkladisteServis.MozeSeDodati(naStanju, vecUKorpi, kolicina);

            Assert.IsTrue(rezultat);
        }

        // Data driven (DataRow): nema dovoljno na stanju (i kad proizvoda uopste nema)
        [TestMethod]
        [DataRow(5, 5, 1)]
        [DataRow(0, 0, 1)]
        [DataRow(10, 3, 8)]
        public void MozeSeDodati_NemaDovoljnoNaStanju_VracaFalse(int naStanju, int vecUKorpi, int kolicina)
        {
            var rezultat = SkladisteServis.MozeSeDodati(naStanju, vecUKorpi, kolicina);

            Assert.IsFalse(rezultat);
        }

        // Test izuzetka: kolicina koja se dodaje mora biti pozitivna
        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void MozeSeDodati_KolicinaNijePozitivna_BacaArgumentOutOfRangeException(int kolicina)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SkladisteServis.MozeSeDodati(5, 0, kolicina));
        }
    }
}
