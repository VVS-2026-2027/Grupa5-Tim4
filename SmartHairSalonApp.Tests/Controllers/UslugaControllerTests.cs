using System.Globalization;
using System.Xml.Linq;
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
    public class UslugaControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        // Podaci za DynamicData: validne usluge iz XML fajla Data/usluge.xml
        public static IEnumerable<object[]> UslugeIzXml
        {
            get
            {
                var dokument = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Data", "usluge.xml"));
                return dokument.Root!.Elements("usluga").Select(e => new object[]
                {
                    (string)e.Attribute("naziv")!,
                    double.Parse((string)e.Attribute("cijena")!, CultureInfo.InvariantCulture)
                });
            }
        }

        // Ubacuje salon kroz poseban kontekst, da ga kontroler ne bi vec pratio.
        private void DodajSalon(int id, string naziv = "Salon A")
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Saloni.Add(new Salon { Id = id, Naziv = naziv, Lokacija = "Sarajevo", RadnoVrijeme = "08:00-16:00" });
            kontekst.SaveChanges();
        }

        // Usluga mora imati salon u bazi, jer Include(u => u.Salon) vraca samo usluge ciji salon postoji.
        private void DodajUslugu(int id, string naziv = "Sisanje", double cijena = 15, int salonId = 1)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Usluge.Add(new Usluga { Id = id, Naziv = naziv, Cijena = cijena, SalonId = salonId });
            kontekst.SaveChanges();
        }

        private UslugaController NoviKontroler()
        {
            return new UslugaController(TestDb.Kontekst(_opcije));
        }

        private int BrojUslugaUBazi()
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            return kontekst.Usluge.Count();
        }

        // ---------- Index ----------

        [TestMethod]
        public async Task Index_PostojeDvijeUsluge_VracaViewSaUslugamaISalonom()
        {
            DodajSalon(1, "Salon A");
            DodajUslugu(1, "Sisanje");
            DodajUslugu(2, "Feniranje");

            var rezultat = await NoviKontroler().Index();

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            var model = view.Model as List<Usluga>;
            Assert.IsNotNull(model);
            Assert.HasCount(2, model);
            Assert.AreEqual("Salon A", model[0].Salon.Naziv);
        }

        [TestMethod]
        public async Task Index_NemaUsluga_VracaPraznuListu()
        {
            var rezultat = await NoviKontroler().Index();

            var model = (rezultat as ViewResult)!.Model as List<Usluga>;
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
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(999)]
        public async Task Details_NepostojeciId_VracaNotFound(int id)
        {
            DodajSalon(1);
            DodajUslugu(1);

            var rezultat = await NoviKontroler().Details(id);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Details_PostojeciId_VracaViewSaUslugom()
        {
            DodajSalon(1, "Salon A");
            DodajUslugu(1, "Sisanje", 15);

            var rezultat = await NoviKontroler().Details(1);

            var usluga = (rezultat as ViewResult)?.Model as Usluga;
            Assert.IsNotNull(usluga);
            Assert.AreEqual("Sisanje", usluga.Naziv);
            Assert.AreEqual("Salon A", usluga.Salon.Naziv);
        }

        // ---------- Create ----------

        [TestMethod]
        public void Create_Get_VracaViewIListuSalona()
        {
            DodajSalon(1);
            DodajSalon(2, "Salon B");
            var kontroler = NoviKontroler();

            var rezultat = kontroler.Create();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
            var saloni = kontroler.ViewData["SalonId"] as SelectList;
            Assert.IsNotNull(saloni);
            Assert.HasCount(2, saloni);
        }

        // Data driven (DynamicData + XML): svaka usluga iz fajla se uspjesno dodaje
        [TestMethod]
        [DynamicData(nameof(UslugeIzXml))]
        public async Task Create_ValidnaUslugaIzXml_DodajeUsluguIPreusmjerava(string naziv, double cijena)
        {
            DodajSalon(1);
            var usluga = new Usluga { Naziv = naziv, Cijena = cijena, SalonId = 1 };

            var rezultat = await NoviKontroler().Create(usluga);

            var preusmjerenje = rezultat as RedirectToActionResult;
            Assert.IsNotNull(preusmjerenje);
            Assert.AreEqual("Index", preusmjerenje.ActionName);
            using var kontekst = TestDb.Kontekst(_opcije);
            var sacuvana = kontekst.Usluge.Single();
            Assert.AreEqual(naziv, sacuvana.Naziv);
            Assert.AreEqual(cijena, sacuvana.Cijena, 0.001);
        }

        [TestMethod]
        public async Task Create_NevalidanModel_VracaFormuINeDodajeUslugu()
        {
            DodajSalon(1);
            var kontroler = NoviKontroler();
            kontroler.ModelState.AddModelError("Naziv", "Polje je obavezno.");
            var usluga = new Usluga { Naziv = "", Cijena = 10, SalonId = 1 };

            var rezultat = await kontroler.Create(usluga);

            var view = rezultat as ViewResult;
            Assert.IsNotNull(view);
            Assert.AreSame(usluga, view.Model);
            var saloni = kontroler.ViewData["SalonId"] as SelectList;
            Assert.IsNotNull(saloni);
            Assert.AreEqual(1, (int)saloni.SelectedValue!);
            Assert.AreEqual(0, BrojUslugaUBazi());
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
        public async Task Edit_Get_PostojeciId_VracaViewSaUslugomIListomSalona()
        {
            DodajSalon(1);
            DodajUslugu(1, "Sisanje");
            var kontroler = NoviKontroler();

            var rezultat = await kontroler.Edit(1);

            var usluga = (rezultat as ViewResult)?.Model as Usluga;
            Assert.IsNotNull(usluga);
            Assert.AreEqual(1, usluga.Id);
            Assert.IsNotNull(kontroler.ViewData["SalonId"]);
        }

        // ---------- Edit (POST) ----------

        [TestMethod]
        public async Task Edit_Post_IdSeNePoklapa_VracaNotFound()
        {
            var usluga = new Usluga { Id = 2, Naziv = "Sisanje", Cijena = 15, SalonId = 1 };

            var rezultat = await NoviKontroler().Edit(1, usluga);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }

        [TestMethod]
        public async Task Edit_Post_ValidnaUsluga_MijenjaCijenuIPreusmjerava()
        {
            DodajSalon(1);
            DodajUslugu(1, "Sisanje", 15);
            var izmijenjena = new Usluga { Id = 1, Naziv = "Sisanje", Cijena = 25, SalonId = 1 };

            var rezultat = await NoviKontroler().Edit(1, izmijenjena);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(25, kontekst.Usluge.Single(u => u.Id == 1).Cijena, 0.001);
        }

        [TestMethod]
        public async Task Edit_Post_NevalidanModel_VracaFormu()
        {
            DodajSalon(1);
            DodajUslugu(1);
            var kontroler = NoviKontroler();
            kontroler.ModelState.AddModelError("Naziv", "Polje je obavezno.");
            var usluga = new Usluga { Id = 1, Naziv = "", Cijena = 15, SalonId = 1 };

            var rezultat = await kontroler.Edit(1, usluga);

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
            Assert.IsNotNull(kontroler.ViewData["SalonId"]);
        }

        // Zamjenski objekat (Moq): SaveChangesAsync baca izuzetak konkurentnosti, a usluga ne postoji
        [TestMethod]
        public async Task Edit_Post_IzuzetakKonkurentnostiIUslugaNePostoji_VracaNotFound()
        {
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new UslugaController(mockKontekst.Object);
            var usluga = new Usluga { Id = 5, Naziv = "Sisanje", Cijena = 15, SalonId = 1 };

            var rezultat = await kontroler.Edit(5, usluga);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
            mockKontekst.Verify(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Zamjenski objekat (Moq) + test izuzetka: usluga postoji, pa se izuzetak prosljedjuje dalje
        [TestMethod]
        public async Task Edit_Post_IzuzetakKonkurentnostiIUslugaPostoji_BacaDbUpdateConcurrencyException()
        {
            DodajSalon(1);
            DodajUslugu(1);
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new UslugaController(mockKontekst.Object);
            var usluga = new Usluga { Id = 1, Naziv = "Sisanje", Cijena = 20, SalonId = 1 };

            await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => kontroler.Edit(1, usluga));
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
        public async Task Delete_PostojeciId_VracaViewSaUslugom()
        {
            DodajSalon(1);
            DodajUslugu(1, "Sisanje");

            var rezultat = await NoviKontroler().Delete(1);

            var usluga = (rezultat as ViewResult)?.Model as Usluga;
            Assert.IsNotNull(usluga);
            Assert.AreEqual("Sisanje", usluga.Naziv);
        }

        [TestMethod]
        public async Task DeleteConfirmed_PostojecaUsluga_BriseUsluguIPreusmjerava()
        {
            DodajSalon(1);
            DodajUslugu(1);
            DodajUslugu(2, "Feniranje");

            var rezultat = await NoviKontroler().DeleteConfirmed(1);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(1, BrojUslugaUBazi());
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaUsluga_NistaNeBriseIPreusmjerava()
        {
            DodajSalon(1);
            DodajUslugu(1);

            var rezultat = await NoviKontroler().DeleteConfirmed(999);

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
            Assert.AreEqual(1, BrojUslugaUBazi());
        }
    }
}
