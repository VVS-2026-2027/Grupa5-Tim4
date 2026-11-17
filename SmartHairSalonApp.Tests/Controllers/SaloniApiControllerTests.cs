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
    [TestClass]
    public class SaloniApiControllerTests
    {
        private DbContextOptions<ApplicationDbContext> _opcije = null!;

        [TestInitialize]
        public void Inicijalizacija()
        {
            _opcije = TestDb.NovaBaza();
        }

        private void DodajSalon(int id, string naziv, double? latitude = null, double? longitude = null)
        {
            using var kontekst = TestDb.Kontekst(_opcije);
            kontekst.Saloni.Add(new Salon
            {
                Id = id,
                Naziv = naziv,
                Lokacija = "Sarajevo",
                RadnoVrijeme = "08:00-16:00",
                Latitude = latitude,
                Longitude = longitude
            });
            kontekst.SaveChanges();
        }

        // API vraca anonimne objekte, pa rezultat pretvaramo u JSON i citamo ga kao da smo klijent.
        private async Task<JsonElement> PozoviApi()
        {
            var kontroler = new SaloniApiController(TestDb.Kontekst(_opcije));

            var rezultat = await kontroler.GetSalone();

            var ok = rezultat as OkObjectResult;
            Assert.IsNotNull(ok);
            Assert.AreEqual(200, ok.StatusCode);
            return JsonSerializer.SerializeToElement(ok.Value);
        }

        // Data driven (DataRow): API vraca onoliko salona koliko ih ima u bazi
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(5)]
        public async Task GetSalone_VracaSveSaloneIzBaze(int brojSalona)
        {
            for (int i = 1; i <= brojSalona; i++)
            {
                DodajSalon(i, $"Salon {i}");
            }

            var saloni = await PozoviApi();

            Assert.AreEqual(JsonValueKind.Array, saloni.ValueKind);
            Assert.AreEqual(brojSalona, saloni.GetArrayLength());
        }

        [TestMethod]
        public async Task GetSalone_SalonSaKoordinatama_VracaSvaPolja()
        {
            DodajSalon(1, "Salon Ljepota", 43.8563, 18.4131);

            var salon = (await PozoviApi())[0];

            Assert.AreEqual(1, salon.GetProperty("Id").GetInt32());
            Assert.AreEqual("Salon Ljepota", salon.GetProperty("Naziv").GetString());
            Assert.AreEqual("Sarajevo", salon.GetProperty("Lokacija").GetString());
            Assert.AreEqual("08:00-16:00", salon.GetProperty("RadnoVrijeme").GetString());
            Assert.AreEqual(43.8563, salon.GetProperty("Latitude").GetDouble(), 0.0001);
            Assert.AreEqual(18.4131, salon.GetProperty("Longitude").GetDouble(), 0.0001);
        }

        [TestMethod]
        public async Task GetSalone_SalonBezKoordinata_KoordinateSuNull()
        {
            DodajSalon(1, "Salon bez mape");

            var salon = (await PozoviApi())[0];

            Assert.AreEqual(JsonValueKind.Null, salon.GetProperty("Latitude").ValueKind);
            Assert.AreEqual(JsonValueKind.Null, salon.GetProperty("Longitude").ValueKind);
        }
    }
}
