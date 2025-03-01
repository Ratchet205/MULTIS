using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Core
{
    public class Proxy : IRunnableServer
    {
        private string pathtojson = "";
        private Dictionary<string, WebServerInstance> WebServerInstances = [];

        public void RegisterWebServer(WebServerInstance wsi)
        {
            foreach (var url in wsi.Urls)
            {
                try
                {
                    if (string.IsNullOrEmpty(url)) continue;
                    if (WebServerInstances.ContainsKey(url) && WebServerInstances[url].Id == wsi.Id) continue;
                    if (WebServerInstances.ContainsKey(url))
                    {
                        var alreadyExisting = WebServerInstances[url];
                        Console.WriteLine($"[{wsi.Id}] {url} already Registered with {alreadyExisting.Id}. Ignoring Prefix.");
                    }
                    WebServerInstances.Add(url, wsi);
                } catch (Exception ex)
                {
                    Console.WriteLine($"[Error] {ex}: {ex.Message}");
                }
            }
        }

        public Task Run()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void RegisterServersFromJson(string path) //reading a Multis.Core.WebServerInstanceConfig from a JSON File
        {
            throw new NotImplementedException();
        }
    }
}
