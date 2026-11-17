using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SmartHairSalonApp.Data;

namespace SmartHairSalonApp.Tests.Helpers
{
    /// <summary>
    /// Zajednicke pomocne metode za bazu u testovima.
    /// Baza ne mora biti aktivna: koristi se InMemory baza (fake) i Moq mock konteksta.
    /// </summary>
    public static class TestDb
    {
        /// <summary>
        /// Nova, prazna InMemory baza za svaki test, pa su testovi medjusobno nezavisni.
        /// InMemory baza ne podrzava transakcije (ZavrsiNarudzbu ih koristi), pa se to upozorenje ignorise.
        /// </summary>
        public static DbContextOptions<ApplicationDbContext> NovaBaza()
        {
            return new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        /// <summary>Novi kontekst nad zadatom InMemory bazom.</summary>
        public static ApplicationDbContext Kontekst(DbContextOptions<ApplicationDbContext> opcije)
        {
            return new ApplicationDbContext(opcije);
        }

        /// <summary>
        /// Zamjenski objekat (Moq): kontekst radi nad istom InMemory bazom (CallBase = true),
        /// ali SaveChangesAsync uvijek baca DbUpdateConcurrencyException.
        /// Sluzi za testiranje catch grane u akciji Edit, koju InMemory baza sama ne moze izazvati.
        /// </summary>
        public static Mock<ApplicationDbContext> KontekstKojiBacaIzuzetak(DbContextOptions<ApplicationDbContext> opcije)
        {
            var mock = new Mock<ApplicationDbContext>(opcije) { CallBase = true };
            mock.Setup(k => k.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateConcurrencyException("Simulirana konkurentna izmjena."));
            return mock;
        }
    }
}
