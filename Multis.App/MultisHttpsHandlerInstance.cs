using Multis.Https;
using Multis.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multis.App
{
    public class MultisHttpsHandlerInstance : HttpsHandlerInstance
    {
        public MultisHttpsHandlerInstance(string? _domainName = null) : base(_domainName)
        {
            API = new MultisAPI();
        }

        public MultisHttpsHandlerInstance(string hostName, string certFilePath, string? passwd = null) : base(hostName, certFilePath, passwd)
        {
            API = new MultisAPI();
        }

        protected override async Task ProcessRequestAsync(HttpsListenerContext ctx)
        {
            await ctx.Response!.Send("This is a None-Api MultisWebServerInstance Call!", "text/plain");
        }
    }
}
