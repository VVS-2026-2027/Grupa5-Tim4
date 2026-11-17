# SmartHairSalonApp.Tests - pocetni fajlovi za Task 2 (konacna verzija projekta)

Napravite projekat komandama iz dokumenta "Task 2 - Unit testiranje: upute i podjela",
pa u folder SmartHairSalonApp.Tests kopirajte foldere Helpers, Data i Controllers.

Helpers (zajednicko, ne mijenjati bez dogovora):
- TestDb.cs                 InMemory baza po testu + Moq kontekst kojem SaveChangesAsync baca izuzetak
- TestKorisnik.cs           Moq UserManager<Korisnik> + prijavljeni korisnik sa ulogama na kontroleru
- TestSession.cs            lazna sesija (korpa u ProizvodController)
- CsvDataSourceAttribute.cs DataSource iz CSV fajla

Primjeri:
- Controllers/SalonControllerTests.cs  svih 18 slucajeva za SalonController (DataRow, DynamicData/JSON, CsvDataSource, Moq)
- Controllers/HomeControllerTests.cs   HomeController i ErrorViewModel
- Data/saloni.json, Data/saloni_nevalidni.csv

U SmartHairSalonApp.Tests.csproj dodajte:

  <ItemGroup>
    <None Update="Data\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

Obrisite Test1.cs / UnitTest1.cs. Zatim: dotnet test

Primjer za kontroler sa prijavljenim korisnikom (npr. RezervacijaController):

    var opcije = TestDb.NovaBaza();
    var korisnik = new Korisnik { Id = "k1", UserName = "ana", Ime = "Ana", Prezime = "Anic" };
    var userManager = TestKorisnik.UserManager(korisnik);          // Moq
    var kontroler = new RezervacijaController(TestDb.Kontekst(opcije), userManager.Object);
    TestKorisnik.Prijavi(kontroler, "k1", "korisnik");             // uloga za User.IsInRole

    var rezultat = await kontroler.Create(new Rezervacija { UslugaId = 1, ZeljeniTermin = "20.11. u 10:00" });

Napomena: akcije sa .Include(...) (Index, Details) vracaju samo zapise cije povezane
entitete (Korisnik, Usluga, Korpa...) ste prethodno ubacili u bazu. Prvo ubacite korisnika
(kontekst.Users.Add(...)) i uslugu/korpu, pa tek onda rezervaciju/narudzbu.
