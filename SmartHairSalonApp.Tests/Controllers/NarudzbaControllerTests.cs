using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SmartHairSalonApp.Controllers;
using SmartHairSalonApp.Data;
using SmartHairSalonApp.Models;
using SmartHairSalonApp.Tests.Helpers;

namespace SmartHairSalonApp.Tests.Controllers
{
    [TestClass]
    public class NarudzbaControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        // Ubacuje korisnika kroz poseban kontekst, da ga kontroler ne bi vec pratio.
        private void DodajKorisnika(string id)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Users.Add(new Korisnik { Id = id, UserName = id, Ime = "Ime", Prezime = "Prezime" });
            kontekst.SaveChanges();
        }

        // Index koristi Include(Korpa), pa narudzba bez korpe u bazi nije vidljiva.
        // Zato se uz svaku narudzbu ubacuje i korpa istog Id-a.
        private void DodajNarudzbu(int id, string korisnikId, StatusNarudzbe status = StatusNarudzbe.UObradi)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Korpe.Add(new Korpa { Id = id, KorisnikId = korisnikId, UkupnaCijena = 30 });
            kontekst.Narudzbe.Add(new Narudzba { Id = id, KorisnikId = korisnikId, KorpaId = id, StatusNarudzbe = status });
            kontekst.SaveChanges();
        }

        // Kontroler sa prijavljenim korisnikom i zadatom ulogom.
        private NarudzbaController NoviKontroler(string korisnikId, string uloga)
        {
            var korisnik = new Korisnik { Id = korisnikId, UserName = korisnikId };
            var kontroler = new NarudzbaController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(korisnik).Object);
            TestKorisnik.Prijavi(kontroler, korisnikId, uloga);
            return kontroler;
        }

        // Kontroler kod kojeg UserManager ne vraca nijednog korisnika.
        private NarudzbaController KontrolerBezKorisnika()
        {
            var kontroler = new NarudzbaController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(null).Object);
            TestKorisnik.Prijavi(kontroler, "k1", "korisnik");
            return kontroler;
        }

        // Akcija vraca anonimni objekat u JSON-u, pa ga citamo kao da smo klijent.
        private static JsonElement ProcitajJson(IActionResult rezultat)
        {
            var json = rezultat as JsonResult;
            Assert.IsNotNull(json);
            return JsonSerializer.SerializeToElement(json.Value);
        }

        private List<Obavijest> ObavijestiIzBaze()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Obavijesti.ToList();
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_NemaKorisnika_PreusmjeravaNaLoginStranicu()
        {
            var rezultat = await KontrolerBezKorisnika().Index();

            var redirect = rezultat as RedirectToPageResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("/Account/Login", redirect.PageName);
            Assert.IsNotNull(redirect.RouteValues);
            Assert.AreEqual("Identity", redirect.RouteValues["area"]);
        }

        // Data driven (DataRow): sve cetiri varijante naziva uloge koje kontroler prepoznaje
        [TestMethod]
        [DataRow("admin")]
        [DataRow("Admin")]
        [DataRow("zaposlenik")]
        [DataRow("Zaposlenik")]
        public async Task Index_AdminIliZaposlenik_VidiNarudzbeSvihKorisnika(string uloga)
        {
            DodajKorisnika("k1");
            DodajKorisnika("k2");
            DodajNarudzbu(1, "k1");
            DodajNarudzbu(2, "k2");

            var rezultat = await NoviKontroler("osoblje", uloga).Index();

            var model = (rezultat as ViewResult)?.Model as List<Narudzba>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
        }

        [TestMethod]
        public async Task Index_Korisnik_VidiSamoSvojeNarudzbe()
        {
            DodajKorisnika("k1");
            DodajKorisnika("k2");
            DodajNarudzbu(1, "k1");
            DodajNarudzbu(2, "k2");
            DodajNarudzbu(3, "k1");

            var rezultat = await NoviKontroler("k1", "korisnik").Index();

            var model = (rezultat as ViewResult)?.Model as List<Narudzba>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.IsTrue(model.All(n => n.KorisnikId == "k1"));
        }

        [TestMethod]
        public async Task Index_KorisnikNemaNarudzbi_VracaPraznuListu()
        {
            DodajKorisnika("k1");
            DodajKorisnika("k2");
            DodajNarudzbu(1, "k2");

            var rezultat = await NoviKontroler("k1", "korisnik").Index();

            var model = (rezultat as ViewResult)?.Model as List<Narudzba>;
            Assert.IsNotNull(model);
            Assert.AreEqual(0, model.Count);
        }

        [TestMethod]
        public async Task Index_Admin_NarudzbeSuSortiraneOdNajnovije()
        {
            DodajKorisnika("k1");
            DodajNarudzbu(1, "k1");
            DodajNarudzbu(2, "k1");
            DodajNarudzbu(3, "k1");

            var rezultat = await NoviKontroler("a1", "admin").Index();

            var model = (rezultat as ViewResult)?.Model as List<Narudzba>;
            Assert.IsNotNull(model);
            Assert.AreEqual(3, model[0].Id);
            Assert.AreEqual(1, model[2].Id);
        }

        [TestMethod]
        public async Task Index_PostojiNarudzba_UcitavaKorisnikaIKorpu()
        {
            DodajKorisnika("k1");
            DodajNarudzbu(1, "k1");

            var rezultat = await NoviKontroler("k1", "korisnik").Index();

            var narudzba = ((rezultat as ViewResult)?.Model as List<Narudzba>)![0];
            Assert.IsNotNull(narudzba.Korisnik);
            Assert.IsNotNull(narudzba.Korpa);
            Assert.AreEqual(30.0, narudzba.Korpa.UkupnaCijena);
        }

        // ---------- PromijeniStatus ----------

        [TestMethod]
        public async Task PromijeniStatus_NarudzbaNePostoji_VracaNotFound()
        {
            var rezultat = await NoviKontroler("a1", "admin").PromijeniStatus(999, StatusNarudzbe.Potvrdjena);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        // Data driven (DataRow): broj obavijesti zavisi od statusa
        [TestMethod]
        [DataRow(StatusNarudzbe.Potvrdjena, 1)]
        [DataRow(StatusNarudzbe.Odbijena, 1)]
        [DataRow(StatusNarudzbe.UObradi, 0)]
        public async Task PromijeniStatus_RazlicitiStatusi_KreiraOcekivaniBrojObavijesti(StatusNarudzbe status, int ocekivaniBroj)
        {
            DodajKorisnika("k1");
            DodajNarudzbu(1, "k1");

            await NoviKontroler("a1", "admin").PromijeniStatus(1, status);

            Assert.AreEqual(ocekivaniBroj, ObavijestiIzBaze().Count);
        }

        // Data driven (DataRow): tekst obavijesti za statuse koji kreiraju obavijest
        [TestMethod]
        [DataRow(StatusNarudzbe.Potvrdjena, "PRIHVAĆENA")]
        [DataRow(StatusNarudzbe.Odbijena, "ODBIJENA")]
        public async Task PromijeniStatus_PotvrdjenaIliOdbijena_PorukaSadrziOcekivanuRijec(StatusNarudzbe status, string rijec)
        {
            DodajKorisnika("k1");
            DodajNarudzbu(7, "k1");

            await NoviKontroler("a1", "admin").PromijeniStatus(7, status);

            var obavijest = ObavijestiIzBaze().Single();
            StringAssert.Contains(obavijest.Poruka, rijec);
            StringAssert.Contains(obavijest.Poruka, "#7");
        }

        [TestMethod]
        public async Task PromijeniStatus_Potvrdjena_ObavijestJeNamijenjenaKupcu()
        {
            DodajKorisnika("kupac");
            DodajNarudzbu(1, "kupac");

            await NoviKontroler("a1", "admin").PromijeniStatus(1, StatusNarudzbe.Potvrdjena);

            Assert.AreEqual("kupac", ObavijestiIzBaze().Single().KorisnikId);
        }

        [TestMethod]
        [DataRow(StatusNarudzbe.Potvrdjena)]
        [DataRow(StatusNarudzbe.Odbijena)]
        public async Task PromijeniStatus_NoviStatus_SnimaStatusUBazu(StatusNarudzbe status)
        {
            DodajKorisnika("k1");
            DodajNarudzbu(1, "k1", StatusNarudzbe.UObradi);

            await NoviKontroler("a1", "admin").PromijeniStatus(1, status);

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(status, kontekst.Narudzbe.Single().StatusNarudzbe);
        }

        [TestMethod]
        [DataRow(StatusNarudzbe.Potvrdjena, "Potvrdjena")]
        [DataRow(StatusNarudzbe.Odbijena, "Odbijena")]
        [DataRow(StatusNarudzbe.UObradi, "UObradi")]
        public async Task PromijeniStatus_Uspjeh_VracaJsonSaNazivomStatusa(StatusNarudzbe status, string ocekivaniNaziv)
        {
            DodajKorisnika("k1");
            DodajNarudzbu(1, "k1");

            var rezultat = await NoviKontroler("a1", "admin").PromijeniStatus(1, status);

            var json = ProcitajJson(rezultat);
            Assert.IsTrue(json.GetProperty("success").GetBoolean());
            Assert.AreEqual(ocekivaniNaziv, json.GetProperty("statusNaziv").GetString());
        }

        // Zamjenski objekat (Moq): SaveChangesAsync baca izuzetak, a akcija ga ne hvata
        [TestMethod]
        public async Task PromijeniStatus_BazaBacaIzuzetak_BacaDbUpdateConcurrencyException()
        {
            DodajKorisnika("k1");
            DodajNarudzbu(1, "k1");
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new NarudzbaController(mockKontekst.Object, TestKorisnik.UserManager(null).Object);

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(
                () => kontroler.PromijeniStatus(1, StatusNarudzbe.Potvrdjena));
        }

        // Zamjenski objekat (Moq): kad narudzba ne postoji, baza se nikad ne snima
        [TestMethod]
        public async Task PromijeniStatus_NarudzbaNePostoji_NeSnimaPromjene()
        {
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new NarudzbaController(mockKontekst.Object, TestKorisnik.UserManager(null).Object);

            await kontroler.PromijeniStatus(999, StatusNarudzbe.Potvrdjena);

            mockKontekst.Verify(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
