using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Multis.Core
{
    public class WebServerInstance
    {
        private static int _wsiCount = 8080;
        private Proxy _proxyParent;
        private int _id;
        public int Id => _id;
        private readonly List<string> _urls = [];
        public IEnumerable<string> Urls => _urls;

        public WebServerInstance(Proxy proxyParent, params string[] prefixes)
        {
            _proxyParent = proxyParent;
            _id = _wsiCount++;
            _urls.Add($"http://localhost:{_id}");
            foreach(string prefix in prefixes)
            {
                if (_urls.Contains(prefix)) continue;
                _urls.Add(prefix);
            }
            Console.WriteLine($"[{_id}] Registered with:\n");
            foreach(string url in _urls)
            {
                Console.WriteLine($"\t{url}");
            }
        }


        public async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string responseText = "Hello from WebServer!";
                byte[] buffer = Encoding.UTF8.GetBytes(responseText);

                context.Response.ContentType = "text/plain";
                context.Response.ContentLength64 = buffer.Length;

                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request handling error: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }

        public void Add(params string[] prefixes)
        {
            foreach (string prefix in prefixes)
            {
                if (_urls.Contains(prefix)) continue;
                _urls.Add(prefix);
            }
            _proxyParent.RegisterWebServer(this);
        }
    }
}
