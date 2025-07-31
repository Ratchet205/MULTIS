using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multis.Https;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Multis.App
{
    public class MultisAPI : API
    {
        [EndPointPath("/", true)]
        public static async Task<(string content, string mimetype)> Test(HttpsListenerContext ctx)
        {
            string ctn = "This is MultisAPI!";
            string mime = "text/plain";
            return await Task.FromResult((ctn, mime));
        }

        [EndPointPath("/test", true)]
        public static async Task<(string content, string mimetype)> Test2(HttpsListenerContext ctx)
        {
            string ctn = "This is MultisAPI Test2!";
            string mime = "text/plain";
            return await Task.FromResult((ctn, mime));
        }

        public override async Task<(string content, string mimetype)> NotFound()
        {
            string ctn = "{\"404\":\"Warum suchst du so eine Scheiße?\"}";
            string mime = "application/json";

            return await Task.FromResult((ctn, mime));
        }

        [EndPointPath("/v1", true)]
        class V1
        {
            [EndPointPath("/", true)]
            public static async Task<(string content, string mimetype)> V1_Index()
            {
                string ctn = "This is MultisAPI v1!";
                string mime = "text/plain";
                return await Task.FromResult((ctn, mime));
            }

            [EndPointPath("/test", true)]
            public static async Task<(string content, string mimetype)> V1_Test()
            {
                string ctn = "This is MultisAPI v1 Test!";
                string mime = "text/plain";
                return await Task.FromResult((ctn, mime));
            }
        }
    }
}
