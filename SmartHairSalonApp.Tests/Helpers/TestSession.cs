using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace SmartHairSalonApp.Tests.Helpers
{
    /// <summary>
    /// Zamjenski objekat (fake) za sesiju: korpa u ProizvodController se cuva u sesiji,
    /// a ovdje sesija zivi samo u memoriji testa.
    /// </summary>
    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _podaci = new();

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _podaci.Keys;

        public void Clear() => _podaci.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _podaci.Remove(key);
        public void Set(string key, byte[] value) => _podaci[key] = value;
        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _podaci.TryGetValue(key, out value);
    }
}
