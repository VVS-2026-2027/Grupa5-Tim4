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
    public class SalonControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        // Podaci za DynamicData: validni saloni iz JSON fajla Data/saloni.json
        public static IEnumerable<object[]> SaloniIzJsona
        {
            get
            {
                var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "saloni.json"));
                var saloni = JsonSerializer.Deserialize<List<Salon>>(json)!;
                return saloni.Select(s => new object[] { s.Naziv, s.Lokacija, s.RadnoVrijeme });
            }
        }

        // Ubacuje salon kroz poseban kontekst, da ga kontroler ne bi vec pratio.
        private void DodajSalon(int id, string naziv = "Salon A")
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Saloni.Add(new Salon { Id = id, Naziv = naziv, Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" });
            kontekst.SaveChanges();
        }

        private SalonController NoviKontroler()
        {
            return new SalonController(TestDb.Kontekst(_opcije));
        }

        private int BrojSalonaUBazi()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Saloni.Count();
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_PostojeDvaSalona_VracaViewSaObaSalona()
        {
            DodajSalon(1, "Salon A");
            DodajSalon(2, "Salon B");

            var rezultat = await NoviKontroler().Index();

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var model = view.Model as List<Salon>;
            Assert.IsNotNull(model);
            Assert.HasCount(2, model);
        }

        // ---------- Details ----------

        [TestMethod]
        public async Task Details_IdJeNull_VracaNotFound()
        {
            var rezultat = await NoviKontroler().Details(null);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(999)]
        public async Task Details_NepostojeciId_VracaNotFound(int id)
        {
            DodajSalon(1);

            var rezultat = await NoviKontroler().Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaTimSalonom()
        {
            DodajSalon(1, "Salon A");

            var rezultat = await NoviKontroler().Details(1);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var model = view.Model as Salon;
            Assert.IsNotNull(model);
            Assert.AreEqual("Salon A", model.Naziv);
        }

        // ---------- Create ----------

        [TestMethod]
        public void Create_Get_VracaView()
        {
            var rezultat = NoviKontroler().Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        [DynamicData(nameof(SaloniIzJsona))]
        public async Task Create_ValidanSalonIzJsona_SpremaUBazuIPreusmjeravaNaIndex(string naziv, string lokacija, string radnoVrijeme)
        {
            var salon = new Salon { Naziv = naziv, Lokacija = lokacija, RadnoVrijeme = radnoVrijeme };

            var rezultat = await NoviKontroler().Create(salon);

            var redirect = rezultat as RedirectToActionResult;
            Assert.IsNotNull(redirect);
            Assert.AreEqual("Index", redirect.ActionName);
            Assert.AreEqual(1, BrojSalonaUBazi());
        }

        [TestMethod]
        [CsvDataSource("Data/saloni_nevalidni.csv")]
        public async Task Create_NevalidanSalonIzCsv_VracaFormuINistaNeSprema(string naziv, string lokacija, string radnoVrijeme, string nevalidnoPolje)
        {
            var kontroler = NoviKontroler();
            kontroler.ModelState.AddModelError(nevalidnoPolje, "Polje je obavezno.");
            var salon = new Salon { Naziv = naziv, Lokacija = lokacija, RadnoVrijeme = radnoVrijeme };

            var rezultat = await kontroler.Create(salon);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(salon, view.Model);
            Assert.AreEqual(0, BrojSalonaUBazi());
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
        public async Task Edit_Get_PostojeciId_VracaViewSaSalonom()
        {
            DodajSalon(1, "Salon A");

            var rezultat = await NoviKontroler().Edit(1);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreEqual(1, (view.Model as Salon)!.Id);
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var salon = new Salon { Id = 2, Naziv = "Salon", Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" };

            var rezultat = await NoviKontroler().Edit(1, salon);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_ValidanSalon_MijenjaNazivIPreusmjerava()
        {
            DodajSalon(1, "Stari naziv");
            var izmijenjen = new Salon { Id = 1, Naziv = "Novi naziv", Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" };

            var rezultat = await NoviKontroler().Edit(1, izmijenjen);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual("Novi naziv", kontekst.Saloni.Single(s => s.Id == 1).Naziv);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaFormu()
        {
            DodajSalon(1);
            var kontroler = NoviKontroler();
            kontroler.ModelState.AddModelError("Naziv", "Polje je obavezno.");
            var salon = new Salon { Id = 1, Naziv = "", Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" };

            var rezultat = await kontroler.Edit(1, salon);

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        // Zamjenski objekat (Moq): SaveChangesAsync baca izuzetak konkurentnosti
        [TestMethod]
        public async Task Edit_Post_IzuzetakKonkurentnostiISalonNePostoji_VracaNotFound()
        {
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new SalonController(mockKontekst.Object);
            var salon = new Salon { Id = 5, Naziv = "Salon", Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" };

            var rezultat = await kontroler.Edit(5, salon);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
            mockKontekst.Verify(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Zamjenski objekat (Moq) + test izuzetka: salon postoji, pa se izuzetak prosljedjuje dalje
        [TestMethod]
        public async Task Edit_Post_IzuzetakKonkurentnostiISalonPostoji_BacaDbUpdateConcurrencyException()
        {
            DodajSalon(1);
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new SalonController(mockKontekst.Object);
            var salon = new Salon { Id = 1, Naziv = "Salon", Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" };

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => kontroler.Edit(1, salon));
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
        public async Task Delete_PostojeciId_VracaViewSaSalonom()
        {
            DodajSalon(1);

            var rezultat = await NoviKontroler().Delete(1);

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojeciSalon_BriseGaIPreusmjerava()
        {
            DodajSalon(1);

            var rezultat = await NoviKontroler().DeleteConfirmed(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(0, BrojSalonaUBazi());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojeciSalon_SamoPreusmjerava()
        {
            DodajSalon(1);

            var rezultat = await NoviKontroler().DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(1, BrojSalonaUBazi());
        }
    }
}
