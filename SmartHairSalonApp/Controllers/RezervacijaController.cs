using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartHairSalonApp.Data;
using SmartHairSalonApp.Models;
using SmartHairSalonApp.Services;

namespace SmartHairSalonApp.Controllers
{
    public class RezervacijaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Korisnik> _userManager;

        private const string GreskaFormat = "Termin mora biti u formatu dd.MM.yyyy u HH:mm.";
        private const string GreskaProslost = "Termin mora biti u budućnosti.";
        private const string GreskaZauzet = "Termin je već zauzet.";

        public RezervacijaController(
            ApplicationDbContext context,
            UserManager<Korisnik> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Rezervacija
        [Authorize(Roles = "admin,zaposlenik,korisnik")]
        public async Task<IActionResult> Index()
        {
            var korisnik = await _userManager.GetUserAsync(User);
            ViewBag.Admin = User.IsInRole("admin");
            ViewBag.Zaposlenik = User.IsInRole("zaposlenik");
            ViewBag.Korisnik = User.IsInRole("korisnik");

            if (User.IsInRole("korisnik"))
            {
                var mojeRezervacije = await _context.Rezervacije
                    .Include(r => r.Korisnik)
                    .Include(r => r.Usluga)
                    .Where(r => r.KorisnikId == korisnik.Id)
                    .ToListAsync();

                return View(mojeRezervacije);
            }

            var sveRezervacije = await _context.Rezervacije
                .Include(r => r.Korisnik)
                .Include(r => r.Usluga)
                .ToListAsync();

            return View(sveRezervacije);
        }

        // GET: Rezervacija/Details/5
        [Authorize(Roles = "admin,zaposlenik,korisnik")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rezervacija = await _context.Rezervacije
                .Include(r => r.Korisnik)
                .Include(r => r.Usluga)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (rezervacija == null)
            {
                return NotFound();
            }

            return View(rezervacija);
        }

        // GET: Rezervacija/Create
        // 🔥 POPRAVLJENO: Puštamo admina i zaposlenika unutra kako bi im se renderovao naš "Management Access Only" pogled
        [Authorize(Roles = "korisnik,admin,zaposlenik")]
        public IActionResult Create()
        {
            PostaviListuUsluga();
            return View();
        }

        // POST: Rezervacija/Create
        [Authorize(Roles = "korisnik,admin,zaposlenik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UslugaId,ZeljeniTermin")] Rezervacija rezervacija)
        {
            if (User.IsInRole("admin") || User.IsInRole("zaposlenik"))
            {
                return RedirectToAction("Index", "Home");
            }

            // Provjera termina prije snimanja (TDD, ciklus 4)
            var termin = TerminServis.Parsiraj(rezervacija.ZeljeniTermin);
            if (termin == null)
            {
                return FormaSaGreskomTermina(rezervacija, GreskaFormat);
            }

            if (!TerminServis.JeUBuducnosti(termin.Value, DateTime.Now))
            {
                return FormaSaGreskomTermina(rezervacija, GreskaProslost);
            }

            var rezervacijeZaUslugu = await _context.Rezervacije
                .Where(r => r.UslugaId == rezervacija.UslugaId)
                .ToListAsync();
            if (!TerminServis.JeSlobodan(rezervacijeZaUslugu, rezervacija.UslugaId, termin.Value))
            {
                return FormaSaGreskomTermina(rezervacija, GreskaZauzet);
            }

            var korisnik = await _userManager.GetUserAsync(User);

            rezervacija.KorisnikId = korisnik.Id;
            rezervacija.StatusRezervacije = StatusRezervacije.UObradi;

            _context.Rezervacije.Add(rezervacija);
            await _context.SaveChangesAsync();

            // 🔥 IMPLEMENTACIJA OBAVIJESTI ZA ZAPOSLENIKE (Layout prepoznaje "SALON:")
            var obavijestZaOsoblje = new Obavijest
            {
                Poruka = $"SALON: Novi zahtjev za termin od korisnika <b>{korisnik.UserName}</b> za {rezervacija.ZeljeniTermin}.",
                Datum = DateTime.Now,
                KorisnikId = korisnik.Id
            };

            _context.Obavijesti.Add(obavijestZaOsoblje);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home");
        }

        // POTVRDI REZERVACIJU
        [Authorize(Roles = "admin,zaposlenik")]
        public async Task<IActionResult> Prihvati(int id)
        {
            var rezervacija = await _context.Rezervacije.FindAsync(id);
            if (rezervacija == null) return NotFound();

            rezervacija.StatusRezervacije = StatusRezervacije.Potvrdjena;

            string terminTekst = rezervacija.ZeljeniTermin ?? "";

            // 🔥 OBAVIJEST KUPCU DA MU JE TERMIN ODOBREN
            var obavijestKupac = new Obavijest
            {
                Poruka = $"Vaš zahtjev za termin ({terminTekst}) je <span class='text-success fw-bold'>ODOBREN</span>!",
                Datum = DateTime.Now,
                KorisnikId = rezervacija.KorisnikId
            };
            _context.Obavijesti.Add(obavijestKupac);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ODBIJ REZERVACIJU
        [Authorize(Roles = "admin,zaposlenik")]
        public async Task<IActionResult> Odbij(int id)
        {
            var rezervacija = await _context.Rezervacije.FindAsync(id);
            if (rezervacija == null) return NotFound();

            rezervacija.StatusRezervacije = StatusRezervacije.Odbijena;

            string terminTekst = rezervacija.ZeljeniTermin ?? "";

            // 🔥 OBAVIJEST KUPCU DA MU JE TERMIN ODBIJEN
            var obavijestKupac = new Obavijest
            {
                Poruka = $"Vaš zahtjev za termin ({terminTekst}) je nažalost <span class='text-danger fw-bold'>ODBIJEN</span>. Molimo pokušajte drugi termin.",
                Datum = DateTime.Now,
                KorisnikId = rezervacija.KorisnikId
            };
            _context.Obavijesti.Add(obavijestKupac);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Rezervacija/Edit/5
        [Authorize(Roles = "admin,zaposlenik")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rezervacija = await _context.Rezervacije.FindAsync(id);

            if (rezervacija == null)
            {
                return NotFound();
            }

            ViewData["KorisnikId"] =
                new SelectList(_context.Users, "Id", "Id", rezervacija.KorisnikId);

            ViewData["UslugaId"] =
                new SelectList(_context.Usluge, "Id", "Naziv", rezervacija.UslugaId);

            return View(rezervacija);
        }

        // POST: Rezervacija/Edit/5
        [Authorize(Roles = "admin,zaposlenik")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,StatusRezervacije,KorisnikId,UslugaId,ZeljeniTermin")]
    Rezervacija rezervacija)
        {
            if (id != rezervacija.Id)
            {
                return NotFound();
            }

            // 🔥 NOVO: Ako admin pokuša poslati formu za izmjenu statusa, backend ga blokira!
            if (User.IsInRole("admin") || User.IsInRole("Admin"))
            {
                ModelState.AddModelError("", "Admin nema ovlasti za mijenjanje statusa rezervacije. To radi isključivo zaposlenik.");

                // Vraćamo ga na isti pogled sa ispisanom greškom na vrhu forme
                ViewData["KorisnikId"] = new SelectList(_context.Users, "Id", "Id", rezervacija.KorisnikId);
                ViewData["UslugaId"] = new SelectList(_context.Usluge, "Id", "Naziv", rezervacija.UslugaId);
                return View(rezervacija);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    string terminTekst = rezervacija.ZeljeniTermin ?? "";

                    if (rezervacija.StatusRezervacije == StatusRezervacije.Potvrdjena)
                    {
                        var obavijestKupac = new Obavijest
                        {
                            Poruka = $"Vaš zahtjev za termin ({terminTekst}) je <span class='text-success fw-bold'>ODOBREN</span>!",
                            Datum = DateTime.Now,
                            KorisnikId = rezervacija.KorisnikId
                        };
                        _context.Obavijesti.Add(obavijestKupac);
                    }
                    else if (rezervacija.StatusRezervacije == StatusRezervacije.Odbijena)
                    {
                        var obavijestKupac = new Obavijest
                        {
                            Poruka = $"Vaš zahtjev za termin ({terminTekst}) je nažalost <span class='text-danger fw-bold'>ODBIJEN</span>. Molimo pokušajte drugi termin.",
                            Datum = DateTime.Now,
                            KorisnikId = rezervacija.KorisnikId
                        };
                        _context.Obavijesti.Add(obavijestKupac);
                    }

                    _context.Update(rezervacija);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RezervacijaExists(rezervacija.Id))
                    {
                        return NotFound();
                    }
                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["KorisnikId"] =
                new SelectList(_context.Users, "Id", "Id", rezervacija.KorisnikId);

            ViewData["UslugaId"] =
                new SelectList(_context.Usluge, "Id", "Naziv", rezervacija.UslugaId);

            return View(rezervacija);
        }

        // GET: Rezervacija/Delete/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rezervacija = await _context.Rezervacije
                .Include(r => r.Korisnik)
                .Include(r => r.Usluga)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (rezervacija == null)
            {
                return NotFound();
            }

            return View(rezervacija);
        }

        // POST: Rezervacija/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rezervacija = await _context.Rezervacije.FindAsync(id);

            if (rezervacija != null)
            {
                _context.Rezervacije.Remove(rezervacija);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Lista usluga za padajuci meni na formi za rezervaciju
        private void PostaviListuUsluga()
        {
            ViewData["UslugaId"] = new SelectList(_context.Usluge, "Id", "Naziv");
        }

        // Vraca formu za rezervaciju sa greskom na polju ZeljeniTermin; nista se ne snima
        private IActionResult FormaSaGreskomTermina(Rezervacija rezervacija, string greska)
        {
            ModelState.AddModelError(nameof(Rezervacija.ZeljeniTermin), greska);
            PostaviListuUsluga();
            return View(rezervacija);
        }

        private bool RezervacijaExists(int id)
        {
            return _context.Rezervacije.Any(e => e.Id == id);
        }
    }
}