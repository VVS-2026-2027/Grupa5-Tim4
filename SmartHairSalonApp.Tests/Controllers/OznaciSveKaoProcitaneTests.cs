using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmartHairSalonApp.Controllers;
using SmartHairSalonApp.Data;
using SmartHairSalonApp.Models;
using SmartHairSalonApp.Tests.Helpers;

namespace SmartHairSalonApp.Tests.Controllers
{
    // Testovi za ProizvodController.OznaciSveKaoProcitane
    [TestClass]
    public class OznaciSveKaoProcitaneTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        private static Korisnik Kupac()
        {
            return new Korisnik { Id = "k1", UserName = "ana", Ime = "Ana", Prezime = "Anic" };
        }

        private void DodajObavijest(int id, string poruka, string korisnikId)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Obavijesti.Add(new Obavijest { Id = id, Poruka = poruka, KorisnikId = korisnikId, Datum = DateTime.Now });
            kontekst.SaveChanges();
        }

        // Kontroler sa prijavljenim korisnikom (ili bez njega, ako je korisnik null) i praznom sesijom.
        private (ProizvodController Kontroler, TestSession Sesija) NoviKontroler(Korisnik? korisnik, string uloga = "korisnik")
        {
            var kontroler = new ProizvodController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(korisnik).Object);
            var sesija = TestKorisnik.Prijavi(kontroler, "k1", uloga);
            return (kontroler, sesija);
        }

        private static JsonElement ProcitajJson(IActionResult rezultat)
        {
            var json = rezultat as JsonResult;
            Assert.IsNotNull(json);
            return JsonSerializer.SerializeToElement(json.Value);
        }

        // ---------- Obavijesti ----------
        // Obavijesti u svim testovima (poruka nikad nije null, jer akcija poziva StartsWith):
        //   1: "SALON: ..."  (k1)    2: "ADMIN: ..." (k2)
        //   3: licna (k1)            4: licna (k2)

        private void DodajMjesavinuObavijesti()
        {
            DodajObavijest(1, "SALON: Kreirana je nova narudžba #1.", "k1");
            DodajObavijest(2, "ADMIN: Nova rezervacija čeka obradu.", "k2");
            DodajObavijest(3, "Vaša narudžba #1 je PRIHVAĆENA.", "k1");
            DodajObavijest(4, "Vaša rezervacija je potvrđena.", "k2");
        }

        private List<int> PreostaleObavijestiId()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Obavijesti.Select(o => o.Id).OrderBy(id => id).ToList();
        }

        [TestMethod]
        public async Task OznaciSveKaoProcitane_NemaKorisnika_VracaBadRequest()
        {
            var (kontroler, _) = NoviKontroler(null);

            var rezultat = await kontroler.OznaciSveKaoProcitane();

            Assert.IsInstanceOfType(rezultat, typeof(BadRequestResult));
        }

        // Data driven (DataRow): koliko obavijesti ostane u bazi zavisi od uloge
        [TestMethod]
        [DataRow("admin", 2)]
        [DataRow("Admin", 2)]
        [DataRow("zaposlenik", 3)]
        [DataRow("Zaposlenik", 3)]
        [DataRow("korisnik", 3)]
        public async Task OznaciSveKaoProcitane_RazlicitaUloga_OstajeOcekivaniBrojObavijesti(string uloga, int ocekivanoPreostalo)
        {
            DodajMjesavinuObavijesti();
            var (kontroler, _) = NoviKontroler(Kupac(), uloga);

            await kontroler.OznaciSveKaoProcitane();

            Assert.AreEqual(ocekivanoPreostalo, PreostaleObavijestiId().Count);
        }

        [TestMethod]
        public async Task OznaciSveKaoProcitane_Admin_BriseSalonIAdminPoruke()
        {
            DodajMjesavinuObavijesti();
            var (kontroler, _) = NoviKontroler(Kupac(), "admin");

            await kontroler.OznaciSveKaoProcitane();

            CollectionAssert.AreEqual(new List<int> { 3, 4 }, PreostaleObavijestiId());
        }

        [TestMethod]
        public async Task OznaciSveKaoProcitane_Zaposlenik_BriseSamoSalonPoruke()
        {
            DodajMjesavinuObavijesti();
            var (kontroler, _) = NoviKontroler(Kupac(), "zaposlenik");

            await kontroler.OznaciSveKaoProcitane();

            CollectionAssert.AreEqual(new List<int> { 2, 3, 4 }, PreostaleObavijestiId());
        }

        [TestMethod]
        public async Task OznaciSveKaoProcitane_Korisnik_BriseSamoSvojeLicneObavijesti()
        {
            DodajMjesavinuObavijesti();
            var (kontroler, _) = NoviKontroler(Kupac(), "korisnik");

            await kontroler.OznaciSveKaoProcitane();

            CollectionAssert.AreEqual(new List<int> { 1, 2, 4 }, PreostaleObavijestiId());
        }

        [TestMethod]
        [DataRow("admin")]
        [DataRow("zaposlenik")]
        [DataRow("korisnik")]
        public async Task OznaciSveKaoProcitane_NemaObavijesti_VracaSuccessTrue(string uloga)
        {
            var (kontroler, _) = NoviKontroler(Kupac(), uloga);

            var rezultat = await kontroler.OznaciSveKaoProcitane();

            Assert.IsTrue(ProcitajJson(rezultat).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task OznaciSveKaoProcitane_PostojeObavijesti_VracaSuccessTrue()
        {
            DodajMjesavinuObavijesti();
            var (kontroler, _) = NoviKontroler(Kupac(), "admin");

            var rezultat = await kontroler.OznaciSveKaoProcitane();

            Assert.IsTrue(ProcitajJson(rezultat).GetProperty("success").GetBoolean());
        }
    }
}
