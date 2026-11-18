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
    // Podaci iz Data/korpe.json
    public class StavkaPodaci
    {
        public double Cijena { get; set; }
        public int Kolicina { get; set; }
    }

    public class KorpaPodaci
    {
        public string Opis { get; set; } = string.Empty;
        public List<StavkaPodaci> Stavke { get; set; } = new();
        public double OcekivaniTotal { get; set; }

        // Naziv testa u Test Exploreru
        public override string ToString() => Opis;
    }

    // Testovi za ProizvodController.ZavrsiNarudzbu
    [TestClass]
    public class ZavrsiNarudzbuTests
    {
        private const double CijenaProizvoda = 20;
        private const int StanjeProizvoda = 10;
        private const int KomadaUKorpi = 2;
        private const string KljucKorpe = "MojaKorpa";

        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        // Podaci za DynamicData: korpe i ocekivani total iz JSON fajla Data/korpe.json
        public static IEnumerable<object[]> KorpeIzJsona
        {
            get
            {
                var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "korpe.json"));
                var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var korpe = JsonSerializer.Deserialize<List<KorpaPodaci>>(json, opcije)!;
                return korpe.Select(k => new object[] { k });
            }
        }

        // ---------- pomocne metode ----------

        private static Korisnik Kupac()
        {
            return new Korisnik { Id = "k1", UserName = "ana", Ime = "Ana", Prezime = "Anic" };
        }

        // Proizvod ubacujemo kroz poseban kontekst, da ga kontroler ne bi vec pratio.
        private void DodajProizvod(int id, double cijena = CijenaProizvoda, int kolicina = StanjeProizvoda)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Proizvodi.Add(new Proizvod { Id = id, Naziv = $"Proizvod {id}", Cijena = cijena, Opis = "Opis", Kolicina = kolicina });
            kontekst.SaveChanges();
        }

        private void ObrisiProizvod(int id)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Proizvodi.Remove(kontekst.Proizvodi.Single(p => p.Id == id));
            kontekst.SaveChanges();
        }

        // Kontroler sa prijavljenim korisnikom (ili bez njega, ako je korisnik null) i praznom sesijom.
        private (ProizvodController Kontroler, TestSession Sesija) NoviKontroler(Korisnik? korisnik, string uloga = "korisnik")
        {
            var kontroler = new ProizvodController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(korisnik).Object);
            var sesija = TestKorisnik.Prijavi(kontroler, "k1", uloga);
            return (kontroler, sesija);
        }

        // Korpu punimo preko DodajUKorpu (jednom) i PromijeniKolicinu (do trazene kolicine).
        private static void NapuniKorpu(ProizvodController kontroler, int proizvodId, int komada)
        {
            kontroler.DodajUKorpu(proizvodId);
            kontroler.PromijeniKolicinu(proizvodId, komada - 1);
        }

        // Korpu postavljamo direktno u sesiju, jer DodajUKorpu vise ne dozvoljava vise komada nego sto ima na stanju.
        private static void PostaviKorpu(TestSession sesija, int proizvodId, string naziv, int komada)
        {
            var korpa = new List<StavkaKorpeViewModel>
            {
                new StavkaKorpeViewModel { ProizvodId = proizvodId, Naziv = naziv, Cijena = CijenaProizvoda, Kolicina = komada }
            };
            sesija.SetString(KljucKorpe, JsonSerializer.Serialize(korpa));
        }

        // Za data driven test: proizvodi dobijaju Id 1, 2, ... a korpa se puni redom.
        private void DodajProizvodeIPunKorpu(ProizvodController kontroler, KorpaPodaci podaci)
        {
            for (int i = 0; i < podaci.Stavke.Count; i++)
            {
                DodajProizvod(i + 1, podaci.Stavke[i].Cijena, 100);
                NapuniKorpu(kontroler, i + 1, podaci.Stavke[i].Kolicina);
            }
        }

        // Uspjesna narudzba: proizvod 1 (20 KM, 10 na stanju), 2 komada u korpi.
        private async Task<(IActionResult Rezultat, TestSession Sesija)> IzvrsiUspjesnuNarudzbu()
        {
            DodajProizvod(1);
            var (kontroler, sesija) = NoviKontroler(Kupac());
            NapuniKorpu(kontroler, 1, KomadaUKorpi);

            var rezultat = await kontroler.ZavrsiNarudzbu();

            return (rezultat, sesija);
        }

        private static JsonElement ProcitajJson(IActionResult rezultat)
        {
            var json = rezultat as JsonResult;
            Assert.IsNotNull(json);
            return JsonSerializer.SerializeToElement(json.Value);
        }

        // ---------- ZavrsiNarudzbu: nema korisnika ----------

        [TestMethod]
        public async Task ZavrsiNarudzbu_NemaKorisnika_VracaSuccessFalse()
        {
            var (kontroler, _) = NoviKontroler(null);

            var rezultat = await kontroler.ZavrsiNarudzbu();

            Assert.IsFalse(ProcitajJson(rezultat).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_NemaKorisnika_VracaRedirectUrlNaLogin()
        {
            var (kontroler, _) = NoviKontroler(null);

            var rezultat = await kontroler.ZavrsiNarudzbu();

            Assert.AreEqual("/Identity/Account/Login", ProcitajJson(rezultat).GetProperty("redirectUrl").GetString());
        }

        // ---------- ZavrsiNarudzbu: prazna korpa ----------

        [TestMethod]
        public async Task ZavrsiNarudzbu_PraznaKorpa_VracaSuccessFalse()
        {
            var (kontroler, _) = NoviKontroler(Kupac());

            var rezultat = await kontroler.ZavrsiNarudzbu();

            Assert.IsFalse(ProcitajJson(rezultat).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_PraznaKorpa_VracaPorukuDaJeKorpaPrazna()
        {
            var (kontroler, _) = NoviKontroler(Kupac());

            var rezultat = await kontroler.ZavrsiNarudzbu();

            Assert.AreEqual("Vaša korpa je prazna.", ProcitajJson(rezultat).GetProperty("message").GetString());
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_PraznaKorpa_NistaNeSnimaUBazu()
        {
            var (kontroler, _) = NoviKontroler(Kupac());

            await kontroler.ZavrsiNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(0, kontekst.Narudzbe.Count());
            Assert.AreEqual(0, kontekst.Korpe.Count());
        }

        // ---------- ZavrsiNarudzbu: uspjeh ----------

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_VracaSuccessTrue()
        {
            var (rezultat, _) = await IzvrsiUspjesnuNarudzbu();

            Assert.IsTrue(ProcitajJson(rezultat).GetProperty("success").GetBoolean());
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_UBaziJeJednaKorpaSaTacnomCijenom()
        {
            await IzvrsiUspjesnuNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            var korpa = kontekst.Korpe.Single();
            Assert.AreEqual(CijenaProizvoda * KomadaUKorpi, korpa.UkupnaCijena, 0.001);
            Assert.AreEqual("k1", korpa.KorisnikId);
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_SnimaStavkuKorpeSaKolicinom()
        {
            await IzvrsiUspjesnuNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            var stavka = kontekst.KorpaProizvodi.Single();
            Assert.AreEqual(1, stavka.ProizvodId);
            Assert.AreEqual(KomadaUKorpi, stavka.Kolicina);
            Assert.AreEqual(kontekst.Korpe.Single().Id, stavka.KorpaId);
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_SmanjujeKolicinuProizvodaNaStanju()
        {
            await IzvrsiUspjesnuNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(StanjeProizvoda - KomadaUKorpi, kontekst.Proizvodi.Single().Kolicina);
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_KreiraNarudzbuUObradi()
        {
            await IzvrsiUspjesnuNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            var narudzba = kontekst.Narudzbe.Single();
            Assert.AreEqual(StatusNarudzbe.UObradi, narudzba.StatusNarudzbe);
            Assert.AreEqual("k1", narudzba.KorisnikId);
            Assert.AreEqual(kontekst.Korpe.Single().Id, narudzba.KorpaId);
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_KreiraObavijestZaSalon()
        {
            await IzvrsiUspjesnuNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            var obavijest = kontekst.Obavijesti.Single();
            StringAssert.StartsWith(obavijest.Poruka, "SALON:");
            Assert.AreEqual("k1", obavijest.KorisnikId);
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_Uspjeh_PraznaSesiju()
        {
            var (_, sesija) = await IzvrsiUspjesnuNarudzbu();

            Assert.IsFalse(sesija.Keys.Any());
        }

        // Data driven (DataRow): stanje se smanji za onoliko komada koliko je bilo u korpi
        [TestMethod]
        [DataRow(1)]
        [DataRow(3)]
        [DataRow(10)]
        public async Task ZavrsiNarudzbu_RazlicitaKolicina_SmanjujeStanjeZaKupljeniBroj(int komada)
        {
            DodajProizvod(1, CijenaProizvoda, StanjeProizvoda);
            var (kontroler, _) = NoviKontroler(Kupac());
            NapuniKorpu(kontroler, 1, komada);

            await kontroler.ZavrsiNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(StanjeProizvoda - komada, kontekst.Proizvodi.Single().Kolicina);
        }

        // Data driven (DynamicData + JSON): total u bazi je cijena sa popustom iz Data/korpe.json
        [TestMethod]
        [DynamicData(nameof(KorpeIzJsona))]
        public async Task ZavrsiNarudzbu_KorpaIzJsona_UBaziJeOcekivaniTotal(KorpaPodaci podaci)
        {
            var (kontroler, _) = NoviKontroler(Kupac());
            DodajProizvodeIPunKorpu(kontroler, podaci);

            await kontroler.ZavrsiNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(podaci.OcekivaniTotal, kontekst.Korpe.Single().UkupnaCijena, 0.001);
        }

        // TDD: izmijenjen postojeci test. Proizvod obrisan nakon dodavanja u korpu se sada
        // racuna kao "nedostaje", pa narudzba ne prolazi (ranije je vracala success = true).
        // Korpu puni jedan kontroler, a narudzbu zavrsava drugi (sa istom sesijom).
        [TestMethod]
        public async Task ZavrsiNarudzbu_ProizvodObrisanNakonDodavanjaUKorpu_VracaSuccessFalseINistaNeSnima()
        {
            DodajProizvod(1);
            var (kontrolerZaKorpu, _) = NoviKontroler(Kupac());
            NapuniKorpu(kontrolerZaKorpu, 1, KomadaUKorpi);
            ObrisiProizvod(1);
            var kontrolerZaNarudzbu = new ProizvodController(TestDb.Kontekst(_opcije), TestKorisnik.UserManager(Kupac()).Object)
            {
                ControllerContext = kontrolerZaKorpu.ControllerContext
            };

            var rezultat = await kontrolerZaNarudzbu.ZavrsiNarudzbu();

            var json = ProcitajJson(rezultat);
            Assert.IsFalse(json.GetProperty("success").GetBoolean());
            Assert.AreEqual("Nema dovoljno na stanju: Proizvod 1", json.GetProperty("message").GetString());
            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(0, kontekst.Korpe.Count());
            Assert.AreEqual(0, kontekst.Narudzbe.Count());
        }

        // ---------- ZavrsiNarudzbu: nema dovoljno na stanju (TDD Funkcionalnost 1, ciklus 2) ----------

        [TestMethod]
        public async Task ZavrsiNarudzbu_ViseNegoNaStanju_VracaSuccessFalseIPorukuSaNazivom()
        {
            DodajProizvod(1, CijenaProizvoda, 1);
            var (kontroler, sesija) = NoviKontroler(Kupac());
            PostaviKorpu(sesija, 1, "Proizvod 1", 2);

            var rezultat = await kontroler.ZavrsiNarudzbu();

            var json = ProcitajJson(rezultat);
            Assert.IsFalse(json.GetProperty("success").GetBoolean());
            Assert.AreEqual("Nema dovoljno na stanju: Proizvod 1", json.GetProperty("message").GetString());
        }

        [TestMethod]
        public async Task ZavrsiNarudzbu_ViseNegoNaStanju_NistaNeSnimaUBazu()
        {
            DodajProizvod(1, CijenaProizvoda, 1);
            var (kontroler, sesija) = NoviKontroler(Kupac());
            PostaviKorpu(sesija, 1, "Proizvod 1", 2);

            await kontroler.ZavrsiNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(0, kontekst.Korpe.Count());
            Assert.AreEqual(0, kontekst.Narudzbe.Count());
            Assert.AreEqual(1, kontekst.Proizvodi.Single().Kolicina);
        }

        // ---------- ZavrsiNarudzbu: Moq (zamjenski objekat za bazu) ----------

        // Zamjenski objekat (Moq): SaveChangesAsync baca izuzetak, a akcija ga hvata i vraca poruku
        [TestMethod]
        public async Task ZavrsiNarudzbu_BazaBacaIzuzetak_VracaSuccessFalseIPorukuOGresci()
        {
            DodajProizvod(1);
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new ProizvodController(mockKontekst.Object, TestKorisnik.UserManager(Kupac()).Object);
            TestKorisnik.Prijavi(kontroler, "k1", "korisnik");
            NapuniKorpu(kontroler, 1, KomadaUKorpi);

            var rezultat = await kontroler.ZavrsiNarudzbu();

            var json = ProcitajJson(rezultat);
            Assert.IsFalse(json.GetProperty("success").GetBoolean());
            StringAssert.Contains(json.GetProperty("message").GetString(), "Greška prilikom obrade");
        }

        // Zamjenski objekat (Moq): nakon greske nista nije snimljeno, a snimanje je pokusano samo jednom
        [TestMethod]
        public async Task ZavrsiNarudzbu_BazaBacaIzuzetak_NeKreiraNarudzbuIPokusavaSnimitiJednom()
        {
            DodajProizvod(1);
            var mockKontekst = TestDb.KontekstKojiBacaIzuzetak(_opcije);
            var kontroler = new ProizvodController(mockKontekst.Object, TestKorisnik.UserManager(Kupac()).Object);
            TestKorisnik.Prijavi(kontroler, "k1", "korisnik");
            NapuniKorpu(kontroler, 1, KomadaUKorpi);

            await kontroler.ZavrsiNarudzbu();

            using var kontekst = TestDb.Kontekst(_opcije);
            Assert.AreEqual(0, kontekst.Narudzbe.Count());
            mockKontekst.Verify(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}