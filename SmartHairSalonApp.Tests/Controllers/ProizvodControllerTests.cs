using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
    public class ProizvodControllerTests
    {
        private const string KljucKorpe = "MojaKorpa";
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        // ---------- pomocne metode ----------

        private void DodajProizvod(int id, string? naziv = "Sampon", double cijena = 10, int kolicina = 5)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Proizvodi.Add(new Proizvod { Id = id, Naziv = naziv!, Cijena = cijena, Opis = "Opis", Kolicina = kolicina });
            kontekst.SaveChanges();
        }

        private ProizvodController NoviKontroler()
        {
            return new ProizvodController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(null).Object);
        }

        private (ProizvodController kontroler, TestSession sesija) NoviKontrolerSaKorpom()
        {
            var kontroler = NoviKontroler();
            var sesija = TestKorisnik.Prijavi(kontroler, "k1", "korisnik");
            return (kontroler, sesija);
        }

        private static void PostaviKorpu(TestSession sesija, params StavkaKorpeViewModel[] stavke)
        {
            sesija.SetString(KljucKorpe, JsonSerializer.Serialize(stavke));
        }

        private static StavkaKorpeViewModel Stavka(int proizvodId, int kolicina, double cijena = 10)
        {
            return new StavkaKorpeViewModel { ProizvodId = proizvodId, Naziv = "Sampon", Cijena = cijena, Kolicina = kolicina };
        }

        private static List<StavkaKorpeViewModel> Korpa(IActionResult rezultat)
        {
            return ((rezultat as JsonResult)!.Value as List<StavkaKorpeViewModel>)!;
        }

        private int BrojProizvodaUBazi()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Proizvodi.Count();
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_PostojeDvaProizvoda_VracaViewSaProizvodima()
        {
            DodajProizvod(1);
            DodajProizvod(2, "Balzam");

            var rezultat = await NoviKontroler().Index();

            var model = (rezultat as ViewResult)?.Model as List<Proizvod>;
            Assert.IsNotNull(model);
            Assert.HasCount(2, model);
        }

        [TestMethod]
        public async Task Index_NemaProizvoda_VracaPraznuListu()
        {
            var rezultat = await NoviKontroler().Index();

            var model = (rezultat as ViewResult)?.Model as List<Proizvod>;
            Assert.IsNotNull(model);
            Assert.IsEmpty(model);
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
            DodajProizvod(1);

            var rezultat = await NoviKontroler().Details(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaProizvodom()
        {
            DodajProizvod(1, "Sampon");

            var rezultat = await NoviKontroler().Details(1);

            var proizvod = (rezultat as ViewResult)?.Model as Proizvod;
            Assert.IsNotNull(proizvod);
            Assert.AreEqual("Sampon", proizvod.Naziv);
        }

        [TestMethod]
        public async Task Details_TriProizvodaDrugiTrazen_BrojacSlikeJeDva()
        {
            DodajProizvod(10);
            DodajProizvod(20);
            DodajProizvod(30);
            var kontroler = NoviKontroler();

            await kontroler.Details(20);

            Assert.AreEqual(2, (int)kontroler.ViewBag.BrojacSlike);
        }

        // ---------- Create ----------

        [TestMethod]
        public void Create_Get_VracaView()
        {
            var rezultat = NoviKontroler().Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public async Task Create_ValidanProizvod_DodajeProizvodIPreusmjerava()
        {
            var proizvod = new Proizvod { Naziv = "Sampon", Cijena = 12.5, Opis = "Opis", Kolicina = 3 };

            var rezultat = await NoviKontroler().Create(proizvod);

            var preusmjerenje = rezultat as RedirectToActionResult;
            Assert.IsNotNull(preusmjerenje);
            Assert.AreEqual("Index", preusmjerenje.ActionName);
            Assert.AreEqual(1, BrojProizvodaUBazi());
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaFormuINeDodajeProizvod()
        {
            var kontroler = NoviKontroler();
            kontroler.ModelState.AddModelError("Naziv", "Polje je obavezno.");
            var proizvod = new Proizvod { Naziv = "", Cijena = 10 };

            var rezultat = await kontroler.Create(proizvod);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(proizvod, view.Model);
            Assert.AreEqual(0, BrojProizvodaUBazi());
        }

        // ---------- Edit (GET) ----------

        [TestMethod]
        public async Task Edit_Get_IdJeNull_VracaNotFound()
        {
            var rezultat = await NoviKontroler().Edit(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_NepostojeciId_VracaNotFound()
        {
            var rezultat = await NoviKontroler().Edit(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Get_PostojeciId_VracaViewSaProizvodom()
        {
            DodajProizvod(1, "Sampon");

            var rezultat = await NoviKontroler().Edit(1);

            var proizvod = (rezultat as ViewResult)?.Model as Proizvod;
            Assert.IsNotNull(proizvod);
            Assert.AreEqual(1, proizvod.Id);
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var proizvod = new Proizvod { Id = 2, Naziv = "Sampon" };

            var rezultat = await NoviKontroler().Edit(1, proizvod);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_ValidanProizvod_MijenjaCijenuIPreusmjerava()
        {
            DodajProizvod(1, "Sampon", 10);
            var izmijenjen = new Proizvod { Id = 1, Naziv = "Sampon", Cijena = 20, Opis = "Opis", Kolicina = 5 };

            var rezultat = await NoviKontroler().Edit(1, izmijenjen);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(20, kontekst.Proizvodi.Single(p => p.Id == 1).Cijena, 0.001);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaFormu()
        {
            DodajProizvod(1);
            var kontroler = NoviKontroler();
            kontroler.ModelState.AddModelError("Naziv", "Polje je obavezno.");
            var proizvod = new Proizvod { Id = 1, Naziv = "" };

            var rezultat = await kontroler.Edit(1, proizvod);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(proizvod, view.Model);
        }

        // Zamjenski objekat (Moq): SaveChangesAsync baca izuzetak konkurentnosti, a proizvod ne postoji
        [TestMethod]
        public async Task Edit_Post_IzuzetakKonkurentnostiIProizvodNePostoji_VracaNotFound()
        {
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new ProizvodController(mockKontekst.Object, TestKorisnik.UserManager(null).Object);
            var proizvod = new Proizvod { Id = 5, Naziv = "Sampon" };

            var rezultat = await kontroler.Edit(5, proizvod);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
            mockKontekst.Verify(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Zamjenski objekat (Moq): proizvod postoji, pa se izuzetak prosljedjuje dalje
        [TestMethod]
        public async Task Edit_Post_IzuzetakKonkurentnostiIProizvodPostoji_BacaDbUpdateConcurrencyException()
        {
            DodajProizvod(1);
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new ProizvodController(mockKontekst.Object, TestKorisnik.UserManager(null).Object);
            var proizvod = new Proizvod { Id = 1, Naziv = "Sampon", Cijena = 20 };

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => kontroler.Edit(1, proizvod));
        }

        // ---------- Delete ----------

        [TestMethod]
        public async Task Delete_IdJeNull_VracaNotFound()
        {
            var rezultat = await NoviKontroler().Delete(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_NepostojeciId_VracaNotFound()
        {
            var rezultat = await NoviKontroler().Delete(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Delete_PostojeciId_VracaViewSaProizvodom()
        {
            DodajProizvod(1, "Sampon");

            var rezultat = await NoviKontroler().Delete(1);

            var proizvod = (rezultat as ViewResult)?.Model as Proizvod;
            Assert.IsNotNull(proizvod);
            Assert.AreEqual("Sampon", proizvod.Naziv);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciProizvod_BriseProizvodIPreusmjerava()
        {
            DodajProizvod(1);
            DodajProizvod(2, "Balzam");

            var rezultat = await NoviKontroler().DeleteConfirmed(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(1, BrojProizvodaUBazi());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciProizvod_NistaNeBriseIPreusmjerava()
        {
            DodajProizvod(1);

            var rezultat = await NoviKontroler().DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(1, BrojProizvodaUBazi());
        }

        // ---------- DodajUKorpu ----------

        [TestMethod]
        public void DodajUKorpu_ProizvodNePostoji_VracaNotFound()
        {
            var (kontroler, _) = NoviKontrolerSaKorpom();

            var rezultat = kontroler.DodajUKorpu(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundObjectResult));
        }

        [TestMethod]
        public void DodajUKorpu_NoviProizvod_DodajeStavkuSaKolicinomJedan()
        {
            DodajProizvod(1, "Sampon", 12);
            var (kontroler, _) = NoviKontrolerSaKorpom();

            var korpa = Korpa(kontroler.DodajUKorpu(1));

            Assert.HasCount(1, korpa);
            Assert.AreEqual(1, korpa[0].Kolicina);
            Assert.AreEqual("Sampon", korpa[0].Naziv);
            Assert.AreEqual(12, korpa[0].Cijena, 0.001);
        }

        [TestMethod]
        public void DodajUKorpu_ProizvodBezNaziva_KoristiZadaniNaziv()
        {
            DodajProizvod(1, null);
            var (kontroler, _) = NoviKontrolerSaKorpom();

            var korpa = Korpa(kontroler.DodajUKorpu(1));

            Assert.AreEqual("Proizvod", korpa[0].Naziv);
        }

        [TestMethod]
        public void DodajUKorpu_IstiProizvodDvaput_KolicinaJeDva()
        {
            DodajProizvod(1);
            var (kontroler, _) = NoviKontrolerSaKorpom();
            kontroler.DodajUKorpu(1);

            var korpa = Korpa(kontroler.DodajUKorpu(1));

            Assert.HasCount(1, korpa);
            Assert.AreEqual(2, korpa[0].Kolicina);
        }

        // ---------- PromijeniKolicinu ----------

        [TestMethod]
        [DataRow(1, 3)]
        [DataRow(-1, 1)]
        public void PromijeniKolicinu_KolicinaDva_MijenjaKolicinuZaPromjenu(int promjena, int ocekivanaKolicina)
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, 2));

            var korpa = Korpa(kontroler.PromijeniKolicinu(1, promjena));

            Assert.HasCount(1, korpa);
            Assert.AreEqual(ocekivanaKolicina, korpa[0].Kolicina);
        }

        [TestMethod]
        public void PromijeniKolicinu_KolicinaPadneNaNulu_StavkaNestaje()
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, 1));

            var korpa = Korpa(kontroler.PromijeniKolicinu(1, -1));

            Assert.IsEmpty(korpa);
        }

        [TestMethod]
        public void PromijeniKolicinu_StavkaNePostoji_KorpaOstajeIsta()
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, 2));

            var korpa = Korpa(kontroler.PromijeniKolicinu(999, 1));

            Assert.HasCount(1, korpa);
            Assert.AreEqual(2, korpa[0].Kolicina);
        }

        // ---------- UkloniIzKorpe ----------

        [TestMethod]
        public void UkloniIzKorpe_StavkaPostoji_UklanjaSamoTuStavku()
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, 1), Stavka(2, 1));

            var korpa = Korpa(kontroler.UkloniIzKorpe(1));

            Assert.HasCount(1, korpa);
            Assert.AreEqual(2, korpa[0].ProizvodId);
        }

        [TestMethod]
        public void UkloniIzKorpe_StavkaNePostoji_KorpaOstajeIsta()
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, 1));

            var korpa = Korpa(kontroler.UkloniIzKorpe(999));

            Assert.HasCount(1, korpa);
        }

        // ---------- DohvatiKorpu ----------

        [TestMethod]
        public void DohvatiKorpu_PraznaKorpa_VracaPraznuListu()
        {
            var (kontroler, _) = NoviKontrolerSaKorpom();

            var korpa = Korpa(kontroler.DohvatiKorpu());

            Assert.IsEmpty(korpa);
        }

        [TestMethod]
        public void DohvatiKorpu_PunaKorpa_VracaSveStavke()
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, 1), Stavka(2, 4));

            var korpa = Korpa(kontroler.DohvatiKorpu());

            Assert.HasCount(2, korpa);
            Assert.AreEqual(4, korpa[1].Kolicina);
        }

        // ---------- Basket (data driven: Data/popusti.csv) ----------

        [TestMethod]
        [CsvDataSource("Data/popusti.csv")]
        public void Basket_RazliciteCijeneIKolicine_ObracunavaPopustIPoklon(double cijena, int kolicina, double ocekivaniTotal, bool imaPoklon)
        {
            var (kontroler, sesija) = NoviKontrolerSaKorpom();
            PostaviKorpu(sesija, Stavka(1, kolicina, cijena));

            var rezultat = kontroler.Basket();

            var obracun = (rezultat as ViewResult)?.Model as KorpaObracunViewModel;
            Assert.IsNotNull(obracun);
            Assert.AreEqual(ocekivaniTotal, obracun.Total, 0.001);
            Assert.AreEqual(imaPoklon, obracun.PoklonPoruka.Length > 0);
        }

        [TestMethod]
        public void Basket_PraznaKorpa_VracaPraznuKorpuBezPoklona()
        {
            var (kontroler, _) = NoviKontrolerSaKorpom();

            var obracun = (kontroler.Basket() as ViewResult)?.Model as KorpaObracunViewModel;

            Assert.IsNotNull(obracun);
            Assert.IsEmpty(obracun.Stavke);
            Assert.AreEqual(0, obracun.Total, 0.001);
            Assert.AreEqual(string.Empty, obracun.PoklonPoruka);
        }
    }
}
