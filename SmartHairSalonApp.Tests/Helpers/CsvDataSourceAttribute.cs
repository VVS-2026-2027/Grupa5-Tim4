using System.Globalization;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SmartHairSalonApp.Tests.Helpers
{
    /// <summary>
    /// DataSource za .NET: originalni MSTest [DataSource] radi samo na .NET Frameworku,
    /// pa ovaj atribut (preko interfejsa ITestDataSource) cita testne podatke iz CSV fajla.
    /// Prvi red je zaglavlje, separator je ';'. Za svaki red test se pokrece jednom,
    /// a kolone se redom pretvaraju u tipove parametara test metode.
    /// Upotreba: [TestMethod] [CsvDataSource("Data/fajl.csv")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CsvDataSourceAttribute : Attribute, ITestDataSource
    {
        public string Putanja { get; }

        public CsvDataSourceAttribute(string putanja)
        {
            Putanja = putanja;
        }

        public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
        {
            var parametri = methodInfo.GetParameters();
            var redovi = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, Putanja))
                .Skip(1)
                .Where(red => !string.IsNullOrWhiteSpace(red));

            foreach (var red in redovi)
            {
                var kolone = red.Split(';');
                var vrijednosti = new object?[parametri.Length];
                for (int i = 0; i < parametri.Length; i++)
                {
                    vrijednosti[i] = Pretvori(kolone[i].Trim(), parametri[i].ParameterType);
                }
                yield return vrijednosti;
            }
        }

        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        {
            return data == null ? null : $"{methodInfo.Name} ({string.Join("; ", data)})";
        }

        private static object? Pretvori(string vrijednost, Type tip)
        {
            var stvarniTip = Nullable.GetUnderlyingType(tip) ?? tip;
            if (stvarniTip == typeof(string))
            {
                return vrijednost;
            }
            if (vrijednost.Length == 0)
            {
                return null;
            }
            if (stvarniTip.IsEnum)
            {
                return Enum.Parse(stvarniTip, vrijednost);
            }
            if (stvarniTip == typeof(DateTime))
            {
                return DateTime.Parse(vrijednost, CultureInfo.InvariantCulture);
            }
            return Convert.ChangeType(vrijednost, stvarniTip, CultureInfo.InvariantCulture);
        }
    }
}
