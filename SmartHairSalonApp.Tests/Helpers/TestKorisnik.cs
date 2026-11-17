using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartHairSalonApp.Models;

namespace SmartHairSalonApp.Tests.Helpers
{
    /// <summary>
    /// Pomocne metode za kontrolere koji koriste prijavljenog korisnika, uloge i sesiju
    /// (ProizvodController, RezervacijaController, NarudzbaController).
    /// </summary>
    public static class TestKorisnik
    {
        /// <summary>
        /// Zamjenski objekat (Moq) za UserManager&lt;Korisnik&gt;: GetUserId i GetUserAsync
        /// vracaju zadatog korisnika (ili null), bez prave baze korisnika.
        /// </summary>
        public static Mock<UserManager<Korisnik>> UserManager(Korisnik? korisnik)
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var mock = new Mock<UserManager<Korisnik>>(store.Object, null, null, null, null, null, null, null, null);
            mock.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(korisnik?.Id);
            mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(korisnik);
            return mock;
        }

        /// <summary>
        /// Postavlja na kontroler prijavljenog korisnika sa zadatim ulogama (za User.IsInRole)
        /// i praznu sesiju (za korpu). Primjer: TestKorisnik.Prijavi(kontroler, "k1", "zaposlenik");
        /// </summary>
        public static TestSession Prijavi(Controller kontroler, string korisnikId, params string[] uloge)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, korisnikId) };
            claims.AddRange(uloge.Select(uloga => new Claim(ClaimTypes.Role, uloga)));
            var sesija = new TestSession();
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
                Session = sesija
            };
            kontroler.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return sesija;
        }
    }
}
