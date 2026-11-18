using SmartHairSalonApp.Controllers;
using SmartHairSalonApp.Models;
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

        // ===== Ciklus 2: NedostajuciProizvodi =====

        // Sve ima na stanju: prazna lista
        [TestMethod]
        public void NedostajuciProizvodi_SveImaNaStanju_VracaPraznuListu()
        {
            var korpa = new List<StavkaKorpeViewModel>
            {
                new StavkaKorpeViewModel { ProizvodId = 1, Naziv = "Šampon", Kolicina = 2 },
                new StavkaKorpeViewModel { ProizvodId = 2, Naziv = "Balzam", Kolicina = 1 }
            };
            var proizvodi = new List<Proizvod>
            {
                new Proizvod { Id = 1, Naziv = "Šampon", Kolicina = 5 },
                new Proizvod { Id = 2, Naziv = "Balzam", Kolicina = 1 }
            };

            var rezultat = SkladisteServis.NedostajuciProizvodi(korpa, proizvodi);

            Assert.AreEqual(0, rezultat.Count);
        }

        // Jedna stavka preko stanja: lista sa njenim nazivom
        [TestMethod]
        public void NedostajuciProizvodi_JednaStavkaPrekoStanja_VracaNjenNaziv()
        {
            var korpa = new List<StavkaKorpeViewModel>
            {
                new StavkaKorpeViewModel { ProizvodId = 1, Naziv = "Šampon", Kolicina = 3 },
                new StavkaKorpeViewModel { ProizvodId = 2, Naziv = "Balzam", Kolicina = 1 }
            };
            var proizvodi = new List<Proizvod>
            {
                new Proizvod { Id = 1, Naziv = "Šampon", Kolicina = 2 },
                new Proizvod { Id = 2, Naziv = "Balzam", Kolicina = 5 }
            };

            var rezultat = SkladisteServis.NedostajuciProizvodi(korpa, proizvodi);

            Assert.AreEqual(1, rezultat.Count);
            Assert.AreEqual("Šampon", rezultat[0]);
        }

        // Dvije stavke preko stanja: oba naziva
        [TestMethod]
        public void NedostajuciProizvodi_DvijeStavkePrekoStanja_VracaObaNaziva()
        {
            var korpa = new List<StavkaKorpeViewModel>
            {
                new StavkaKorpeViewModel { ProizvodId = 1, Naziv = "Šampon", Kolicina = 3 },
                new StavkaKorpeViewModel { ProizvodId = 2, Naziv = "Balzam", Kolicina = 4 }
            };
            var proizvodi = new List<Proizvod>
            {
                new Proizvod { Id = 1, Naziv = "Šampon", Kolicina = 2 },
                new Proizvod { Id = 2, Naziv = "Balzam", Kolicina = 1 }
            };

            var rezultat = SkladisteServis.NedostajuciProizvodi(korpa, proizvodi);

            Assert.AreEqual(2, rezultat.Count);
            CollectionAssert.Contains(rezultat, "Šampon");
            CollectionAssert.Contains(rezultat, "Balzam");
        }

        // Proizvod obrisan iz baze: naziv stavke se vraca
        [TestMethod]
        public void NedostajuciProizvodi_ProizvodObrisan_VracaNazivStavke()
        {
            var korpa = new List<StavkaKorpeViewModel>
            {
                new StavkaKorpeViewModel { ProizvodId = 99, Naziv = "Maska", Kolicina = 1 }
            };
            var proizvodi = new List<Proizvod>();

            var rezultat = SkladisteServis.NedostajuciProizvodi(korpa, proizvodi);

            Assert.AreEqual(1, rezultat.Count);
            Assert.AreEqual("Maska", rezultat[0]);
        }

        // Prazna korpa: prazna lista
        [TestMethod]
        public void NedostajuciProizvodi_PraznaKorpa_VracaPraznuListu()
        {
            var rezultat = SkladisteServis.NedostajuciProizvodi(
                new List<StavkaKorpeViewModel>(), new List<Proizvod>());

            Assert.AreEqual(0, rezultat.Count);
        }
    }
}