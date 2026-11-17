using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    public class RezervacijaControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;
        private Korisnik _korisnik = null!;
        private Mock<UserManager<Korisnik>> _userManager = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
            _korisnik = new Korisnik { Id = "k1", UserName = "ana", Ime = "Ana", Prezime = "Anic" };
            _userManager = TestKorisnik.UserManager(_korisnik);
        }

        // ---------- Pomocne metode ----------

        // Kontroler sa prijavljenim korisnikom "k1" i zadatom ulogom.
        private RezervacijaController NoviKontroler(string uloga = "korisnik")
        {
            var kontroler = new RezervacijaController(TestDb.Kontekst(_opcije), _userManager.Object);
            TestKorisnik.Prijavi(kontroler, "k1", uloga);
            return kontroler;
        }

        // Korisnici i usluga moraju postojati u bazi, inace Index/Details/Delete (.Include) ne vrate rezervaciju.
        private void DodajKorisnikeIUslugu()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Users.Add(new Korisnik { Id = "k1", UserName = "ana", Ime = "Ana", Prezime = "Anic" });
            kontekst.Users.Add(new Korisnik { Id = "k2", UserName = "mia", Ime = "Mia", Prezime = "Mic" });
            kontekst.Usluge.Add(new Usluga { Id = 1, Naziv = "Sisanje", Cijena = 20, SalonId = 1 });
            kontekst.SaveChanges();
        }

        // Ubacuje rezervaciju kroz poseban kontekst, da je kontroler vec ne bi pratio.
        private void DodajRezervaciju(int id, string korisnikId = "k1",
            StatusRezervacije status = StatusRezervacije.UObradi, string? termin = "20.11. u 10:00")
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Rezervacije.Add(new Rezervacija
            {
                Id = id,
                KorisnikId = korisnikId,
                UslugaId = 1,
                StatusRezervacije = status,
                ZeljeniTermin = termin
            });
            kontekst.SaveChanges();
        }

        private int BrojRezervacija()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Rezervacije.Count();
        }

        private List<Obavijest> Obavijesti()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Obavijesti.ToList();
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_Korisnik_VidiSamoSvojeRezervacije()
        {
            DodajKorisnikeIUslugu();
            DodajRezervaciju(1, "k1");
            DodajRezervaciju(2, "k2");

            var rezultat = await NoviKontroler("korisnik").Index();

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var model = view.Model as List<Rezervacija>;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.Count);
            Assert.AreEqual("k1", model[0].KorisnikId);
            Assert.AreEqual(false, view.ViewData["Admin"]);
            Assert.AreEqual(false, view.ViewData["Zaposlenik"]);
            Assert.AreEqual(true, view.ViewData["Korisnik"]);
        }

        [TestMethod]
        [DataRow("admin", true, false)]
        [DataRow("zaposlenik", false, true)]
        public async Task Index_AdminIliZaposlenik_VidiSveRezervacije(string uloga, bool jeAdmin, bool jeZaposlenik)
        {
            DodajKorisnikeIUslugu();
            DodajRezervaciju(1, "k1");
            DodajRezervaciju(2, "k2");

            var rezultat = await NoviKontroler(uloga).Index();

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var model = view.Model as List<Rezervacija>;
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.Count);
            Assert.AreEqual(jeAdmin, view.ViewData["Admin"]);
            Assert.AreEqual(jeZaposlenik, view.ViewData["Zaposlenik"]);
            Assert.AreEqual(false, view.ViewData["Korisnik"]);
        }

        // ---------- Details ----------

        [TestMethod]
        public async Task Details_IdJeNull_VracaNotFound()
        {
            var rezultat = await NoviKontroler().Details(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_NepostojeciId_VracaNotFound()
        {
            DodajKorisnikeIUslugu();
            DodajRezervaciju(1);

            var rezultat = await NoviKontroler().Details(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaRezervacijom()
        {
            DodajKorisnikeIUslugu();
            DodajRezervaciju(1);

            var rezultat = await NoviKontroler().Details(1);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var model = view.Model as Rezervacija;
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.Id);
            Assert.AreEqual("Sisanje", model.Usluga.Naziv);
        }

        // ---------- Create (GET) ----------

        [TestMethod]
        public void Create_Get_ListaUslugaJeUViewData()
        {
            DodajKorisnikeIUslugu();

            var rezultat = NoviKontroler().Create();

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var usluge = view.ViewData["UslugaId"] as SelectList;
            Assert.IsNotNull(usluge);
            Assert.AreEqual(1, usluge.Count());
            Assert.AreEqual("Sisanje", usluge.First().Text);
        }

        // ---------- Create (POST) ----------

        [TestMethod]
        [DataRow("admin")]
        [DataRow("zaposlenik")]
        public async Task Create_Post_AdminIliZaposlenik_PreusmjeravaNaHomeINistaNeSprema(string uloga)
        {
            var rezultat = await NoviKontroler(uloga).Create(new Rezervacija { UslugaId = 1, ZeljeniTermin = "20.11. u 10:00" });

            var redirect = rezultat as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Index", redirect.ActionName);
            Assert.AreEqual("Home", redirect.ControllerName);
            Assert.AreEqual(0, BrojRezervacija());
            Assert.AreEqual(0, Obavijesti().Count);
        }

        // Zamjenski objekat (Moq): UserManager vraca prijavljenog korisnika, a Verify provjerava da je pozvan tacno jednom
        [TestMethod]
        public async Task Create_Post_Korisnik_SpremaRezervacijuUObradiIObavijestZaSalon()
        {
            var rezultat = await NoviKontroler("korisnik").Create(new Rezervacija { UslugaId = 1, ZeljeniTermin = "20.11. u 10:00" });

            var redirect = rezultat as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Home", redirect.ControllerName);

            using var kontekst = TestDb.Kontekst(_opcije);
            var rezervacija = kontekst.Rezervacije.Single();
            Assert.AreEqual("k1", rezervacija.KorisnikId);
            Assert.AreEqual(StatusRezervacije.UObradi, rezervacija.StatusRezervacije);

            var obavijest = kontekst.Obavijesti.Single();
            Assert.IsTrue(obavijest.Poruka.StartsWith("SALON:"));
            Assert.IsTrue(obavijest.Poruka.Contains("20.11. u 10:00"));

            _userManager.Verify(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once);
        }

        [TestMethod]
        public async Task Create_Post_TerminJeNull_PorukaSadrziNijeOdabran()
        {
            await NoviKontroler("korisnik").Create(new Rezervacija { UslugaId = 1, ZeljeniTermin = null });

            var obavijest = Obavijesti().Single();
            Assert.IsTrue(obavijest.Poruka.Contains("Nije odabran"));
        }

        // ---------- Prihvati / Odbij ----------

        [TestMethod]
        public async Task Prihvati_NepostojecaRezervacija_VracaNotFound()
        {
            var rezultat = await NoviKontroler("zaposlenik").Prihvati(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Prihvati_PostojecaRezervacija_StatusPotvrdjenaIObavijestKupcu()
        {
            DodajRezervaciju(1, "k2");

            var rezultat = await NoviKontroler("zaposlenik").Prihvati(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(StatusRezervacije.Potvrdjena, kontekst.Rezervacije.Single().StatusRezervacije);
            var obavijest = kontekst.Obavijesti.Single();
            Assert.AreEqual("k2", obavijest.KorisnikId);
            Assert.IsTrue(obavijest.Poruka.Contains("ODOBREN"));
        }

        [TestMethod]
        public async Task Odbij_NepostojecaRezervacija_VracaNotFound()
        {
            var rezultat = await NoviKontroler("zaposlenik").Odbij(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Odbij_PostojecaRezervacija_StatusOdbijenaIObavijestKupcu()
        {
            DodajRezervaciju(1, "k2", termin: null);

            var rezultat = await NoviKontroler("zaposlenik").Odbij(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(StatusRezervacije.Odbijena, kontekst.Rezervacije.Single().StatusRezervacije);
            var obavijest = kontekst.Obavijesti.Single();
            Assert.AreEqual("k2", obavijest.KorisnikId);
            Assert.IsTrue(obavijest.Poruka.Contains("ODBIJEN"));
        }

        // ---------- Edit (GET) ----------

        [TestMethod]
        public async Task Edit_Get_IdJeNull_VracaNotFound()
        {
            var rezultat = await NoviKontroler("zaposlenik").Edit(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_NepostojeciId_VracaNotFound()
        {
            var rezultat = await NoviKontroler("zaposlenik").Edit(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciId_VracaViewSaListama()
        {
            DodajKorisnikeIUslugu();
            DodajRezervaciju(1);

            var rezultat = await NoviKontroler("zaposlenik").Edit(1);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(1, (view.Model as Rezervacija)!.Id);
            Assert.IsInstanceOfType(view.ViewData["KorisnikId"], typeof(SelectList));
            Assert.IsInstanceOfType(view.ViewData["UslugaId"], typeof(SelectList));
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var rezervacija = new Rezervacija { Id = 2, KorisnikId = "k1", UslugaId = 1 };

            var rezultat = await NoviKontroler("zaposlenik").Edit(1, rezervacija);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_Admin_BlokiranVracaFormuSaGreskom()
        {
            DodajRezervaciju(1);
            var kontroler = NoviKontroler("admin");
            var rezervacija = new Rezervacija { Id = 1, KorisnikId = "k1", UslugaId = 1, StatusRezervacije = StatusRezervacije.Potvrdjena };

            var rezultat = await kontroler.Edit(1, rezervacija);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(rezervacija, view.Model);
            Assert.AreEqual(1, kontroler.ModelState.ErrorCount);
            Assert.AreEqual(0, Obavijesti().Count);
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(StatusRezervacije.UObradi, kontekst.Rezervacije.Single().StatusRezervacije);
        }

        [TestMethod]
        [DataRow(StatusRezervacije.Potvrdjena, 1)]
        [DataRow(StatusRezervacije.Odbijena, 1)]
        [DataRow(StatusRezervacije.UObradi, 0)]
        public async Task Edit_Post_Zaposlenik_MijenjaStatusIBrojObavijesti(StatusRezervacije status, int ocekivaniBrojObavijesti)
        {
            DodajRezervaciju(1);
            var rezervacija = new Rezervacija { Id = 1, KorisnikId = "k1", UslugaId = 1, StatusRezervacije = status };

            var rezultat = await NoviKontroler("zaposlenik").Edit(1, rezervacija);

            var redirect = rezultat as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Index", redirect.ActionName);
            Assert.AreEqual(ocekivaniBrojObavijesti, Obavijesti().Count);
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(status, kontekst.Rezervacije.Single().StatusRezervacije);
        }

        // Data driven iz CSV fajla: uloga;status;ocekivaniBrojObavijesti
        [TestMethod]
        [CsvDataSource("Data/rezervacije.csv")]
        public async Task Edit_Post_UlogaIStatusIzCsv_OcekivaniBrojObavijesti(string uloga, StatusRezervacije status, int ocekivaniBrojObavijesti)
        {
            DodajRezervaciju(1);
            var rezervacija = new Rezervacija { Id = 1, KorisnikId = "k1", UslugaId = 1, StatusRezervacije = status };

            await NoviKontroler(uloga).Edit(1, rezervacija);

            Assert.AreEqual(ocekivaniBrojObavijesti, Obavijesti().Count);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaFormuINistaNeMijenja()
        {
            DodajRezervaciju(1);
            var kontroler = NoviKontroler("zaposlenik");
            kontroler.ModelState.AddModelError("UslugaId", "Polje je obavezno.");
            var rezervacija = new Rezervacija { Id = 1, KorisnikId = "k1", UslugaId = 1, StatusRezervacije = StatusRezervacije.Potvrdjena };

            var rezultat = await kontroler.Edit(1, rezervacija);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(rezervacija, view.Model);
            Assert.IsInstanceOfType(view.ViewData["UslugaId"], typeof(SelectList));
            Assert.AreEqual(0, Obavijesti().Count);
        }

        // Zamjenski objekat (Moq): SaveChangesAsync baca izuzetak, a rezervacija ne postoji u bazi
        [TestMethod]
        public async Task Edit_Post_IzuzetakPriSnimanjuIRezervacijaNePostoji_VracaNotFound()
        {
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new RezervacijaController(mockKontekst.Object, _userManager.Object);
            TestKorisnik.Prijavi(kontroler, "k1", "zaposlenik");
            var rezervacija = new Rezervacija { Id = 5, KorisnikId = "k1", UslugaId = 1, StatusRezervacije = StatusRezervacije.UObradi };

            var rezultat = await kontroler.Edit(5, rezervacija);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
            mockKontekst.Verify(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Zamjenski objekat (Moq): rezervacija postoji, pa se izuzetak prosljedjuje dalje
        [TestMethod]
        public async Task Edit_Post_IzuzetakPriSnimanjuIRezervacijaPostoji_BacaDbUpdateConcurrencyException()
        {
            DodajRezervaciju(1);
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new RezervacijaController(mockKontekst.Object, _userManager.Object);
            TestKorisnik.Prijavi(kontroler, "k1", "zaposlenik");
            var rezervacija = new Rezervacija { Id = 1, KorisnikId = "k1", UslugaId = 1, StatusRezervacije = StatusRezervacije.UObradi };

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => kontroler.Edit(1, rezervacija));
        }

        // ---------- Delete ----------

        [TestMethod]
        public async Task Delete_IdJeNull_VracaNotFound()
        {
            var rezultat = await NoviKontroler("admin").Delete(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_NepostojeciId_VracaNotFound()
        {
            var rezultat = await NoviKontroler("admin").Delete(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_PostojeciId_VracaViewSaRezervacijom()
        {
            DodajKorisnikeIUslugu();
            DodajRezervaciju(1);

            var rezultat = await NoviKontroler("admin").Delete(1);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(1, (view.Model as Rezervacija)!.Id);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojecaRezervacija_BriseJeIPreusmjerava()
        {
            DodajRezervaciju(1);

            var rezultat = await NoviKontroler("admin").DeleteConfirmed(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(0, BrojRezervacija());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaRezervacija_SamoPreusmjerava()
        {
            DodajRezervaciju(1);

            var rezultat = await NoviKontroler("admin").DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(1, BrojRezervacija());
        }
    }
}
