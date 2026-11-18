using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHairSalonApp.Controllers;
using SmartHairSalonApp.Data;
using SmartHairSalonApp.Models;
using SmartHairSalonApp.Tests.Helpers;

namespace SmartHairSalonApp.Tests.TDD
{
    // TDD - Funkcionalnost 1, ciklus 1: DodajUKorpu ne dozvoljava vise komada nego sto ima na stanju.
    [TestClass]
    public class DodajUKorpuZalihaTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        private void DodajProizvod(int id, int naStanju)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Proizvodi.Add(new Proizvod { Id = id, Naziv = "Sampon", Cijena = 10, Opis = "Opis", Kolicina = naStanju });
            kontekst.SaveChanges();
        }

        private ProizvodController NoviKontroler()
        {
            var kontroler = new ProizvodController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(null).Object);
            TestKorisnik.Prijavi(kontroler, "k1", "korisnik");
            return kontroler;
        }

        private static List<StavkaKorpeViewModel> Korpa(ProizvodController kontroler)
        {
            return ((kontroler.DohvatiKorpu() as JsonResult)!.Value as List<StavkaKorpeViewModel>)!;
        }

        [TestMethod]
        public void DodajUKorpu_ProizvodNijeNaStanju_VracaBadRequestIKorpaJePrazna()
        {
            DodajProizvod(1, naStanju: 0);
            var kontroler = NoviKontroler();

            var rezultat = kontroler.DodajUKorpu(1);

            Assert.IsInstanceOfType(rezultat, typeof(BadRequestObjectResult));
            Assert.IsEmpty(Korpa(kontroler));
        }

        [TestMethod]
        public void DodajUKorpu_ViseNegoNaStanju_VracaPorukuIKorpaOstajeIsta()
        {
            DodajProizvod(1, naStanju: 1);
            var kontroler = NoviKontroler();
            kontroler.DodajUKorpu(1);

            var rezultat = kontroler.DodajUKorpu(1);

            var greska = rezultat as BadRequestObjectResult;
            Assert.IsNotNull(greska);
            Assert.AreEqual("Nema dovoljno proizvoda na stanju.", greska.Value);
            Assert.AreEqual(1, Korpa(kontroler)[0].Kolicina);
        }

        [TestMethod]
        public void DodajUKorpu_TacnoDoStanja_DodajeKomad()
        {
            DodajProizvod(1, naStanju: 2);
            var kontroler = NoviKontroler();
            kontroler.DodajUKorpu(1);

            var rezultat = kontroler.DodajUKorpu(1);

            Assert.IsInstanceOfType(rezultat, typeof(JsonResult));
            Assert.AreEqual(2, Korpa(kontroler)[0].Kolicina);
        }
    }
}
