using NUnit.Framework;
using Newtonsoft.Json.Linq;
using MULTIS_Engine;

namespace MULTIS_NUnit
{
    [TestFixture]
    public class WebProxyTest
    {
        private MultiWebProxy _server;
        private JObject _json;
        

        [SetUp]
        public void Setup()
        {
            try
            {
                _json = JObject.Parse(@"{""dummy"":{""staticfolder"":""/srv/dummy/public"",""prefixes"":[""http://localhost:8080/""],""routes"":{""/login"":""html/login.html"",""/logout"":""html/logout.html""}}}");
                _server = new MultiWebProxy(_json);

                MultiWebProxy.CreateListener();
                MultiWebProxy.SetUp($"{Worker.workerpath}debug/web-servers");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to initialize MultiWebProxy: {ex.Message}");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_server != null)
            {
                try
                {
                    _server.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to dispose MultiWebProxy: {ex.Message}");
                }
            }
        }

        [Test]
        public void Server_should_contain_Correct_StaticFolder()
        {
            Assert.That(_server.StaticFolder, Is.EqualTo("/srv/dummy/public"));
        }

        [Test]
        public void Server_should_contain_Correct_Prefixes()
        {
            var prefixes = _server.Prefixes;
            Assert.That(prefixes.ToList(), Does.Contain("http://localhost:8080/"));
        }

        [Test]
        public void Server_should_contain_Correct_Routes()
        {
            var routes = _server.Routes;
            Assert.Multiple(() =>
            {
                Assert.That(routes["/login"], Is.EqualTo("html/login.html"));
                Assert.That(routes["/logout"], Is.EqualTo("html/logout.html"));
            });
        }

        [Test]
        public void Server_Listener_Should_Start()
        {
            MultiWebProxy.Start();

            Assert.That(MultiWebProxy.Listener?.IsListening, Is.True);
        }
    }
}
