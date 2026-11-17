using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmartHairSalonApp.Controllers;
using SmartHairSalonApp.Models;

namespace SmartHairSalonApp.Tests.Controllers
{
    [TestClass]
    public class HomeControllerTests
    {
        private static HomeController KontrolerSaTraceId(string traceId)
        {
            var httpContext = new DefaultHttpContext { TraceIdentifier = traceId };
            return new HomeController { ControllerContext = new ControllerContext { HttpContext = httpContext } };
        }

        [TestMethod]
        public void Index_VracaView()
        {
            var rezultat = new HomeController().Index();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public void Privacy_VracaView()
        {
            var rezultat = new HomeController().Privacy();

            Assert.IsInstanceOfType(rezultat, typeof(ViewResult));
        }

        [TestMethod]
        public void Error_NemaAktivnosti_RequestIdJeTraceIdentifier()
        {
            Activity.Current = null;
            var kontroler = KontrolerSaTraceId("trace-123");

            var view = kontroler.Error() as ViewResult;

            Assert.IsNotNull(view);
            Assert.AreEqual("trace-123", (view.Model as ErrorViewModel)!.RequestId);
        }

        [TestMethod]
        public void Error_PostojiAktivnost_RequestIdJeIdAktivnosti()
        {
            var kontroler = KontrolerSaTraceId("trace-123");
            using var aktivnost = new Activity("test").Start();

            var view = kontroler.Error() as ViewResult;

            Assert.IsNotNull(view);
            Assert.AreEqual(aktivnost.Id, (view.Model as ErrorViewModel)!.RequestId);
        }
    }

    [TestClass]
    public class ErrorViewModelTests
    {
        [TestMethod]
        [DataRow("abc-123", true)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void ShowRequestId_ZavisiOdRequestId(string? requestId, bool ocekivano)
        {
            var model = new ErrorViewModel { RequestId = requestId };

            Assert.AreEqual(ocekivano, model.ShowRequestId);
        }
    }
}
