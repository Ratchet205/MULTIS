using Multis.App;
using Multis.Https;
using Multis.Web;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Multis.App
{
    public class Program
    {
        //public static readonly Dictionary<string, Multis.Web.HttpsHandlerInstance> WebServerInstances = [];
        public static async Task Main(string[] args)
        {
            //HttpsHandlerProxy proxy = new();
            //MultisHttpsHandlerInstance lol = new("127.0.0.1", "localhost.pfx", "localhost");
            //OwnServer lol = new();
            //lol.AddDomainName("localhost");
            //Console.WriteLine($"{lol.Certificate!.SerialNumber}");
            //proxy.AddInstance(lol);
            //Console.WriteLine($"{proxy.Instances["localhost"].DomainNames} exists.");
            //await proxy.Run();



            //await TestingMultis.Run();




            //CancellationTokenSource cts = new();
            //TcpListener listener = new(IPAddress.Loopback, 80);
            //listener.Start();
            //Console.WriteLine("waiting for connection");
            //TcpClient persistant = await listener.AcceptTcpClientAsync();
            //Console.WriteLine("got connected");
            //Stream perStream = persistant.GetStream();
            //_ = Task.Run(() =>
            //    {
            //        while (Console.ReadKey(true).KeyChar != 'q') ;
            //        cts.Cancel();
            //    });
            //var req = new HttpsListenerRequest(perStream);
            //int reqCount = 0;
            //while (!cts.Token.IsCancellationRequested)
            //{
            //    await req.ParseRequestAsync();
            //    reqCount++;
            //    Console.WriteLine($"Request: {req.Method} {req.Path} from {persistant.Client.RemoteEndPoint} at [{DateTime.Now}]");
            //    Console.WriteLine($"Request Count: {reqCount}");
            //    HttpsListenerResponse response = new(perStream)
            //    {
            //        StatusCode = HttpStatusCode.OK
            //    };
            //    await response.Send($"[{reqCount}] from {persistant.Client.RemoteEndPoint}", "text/plain");
            //}
            //persistant.Close();






            //HttpsHandlerProxy proxy = new();
            //BashWebExplanatoryInstance bashx = new("localhost")
            //{
            //    Certificate = new X509Certificate2("localhost.pfx", "localhost")
            //};

            //proxy.AddInstance(bashx);

            //_ = proxy.Run();
            //Console.WriteLine("Proxy is running.");
            //await Task.Delay(3000);

            //proxy.Pause();
            //Console.WriteLine("Proxy is paused.");
            //await Task.Delay(3000);

            //Console.WriteLine("Proxy is running again.");
            //await proxy.Run();
            //await Task.Delay(3000);

            //proxy.Shutdown();
            //Console.WriteLine("Proxy is shut down.");

            
            await RecreatingBashwebx.Run();
        }

        private class RecreatingBashwebx
        {
            public static async Task Run()
            {
                HttpsHandlerProxy proxy = new();
                BashWebExplanatoryInstance bashx = new("localhost")
                {
                    Certificate = new X509Certificate2("localhost.pfx", "localhost")
                };

                await proxy.AddInstance(bashx);
                await proxy.Run();
            }
        }

        public class TestingMultis
        {
            public static async Task Run()
            {
                HttpsHandlerProxy proxy = new();
                OwnWebServer ws = new()
                {
                    API = new WebServerApi()
                };
                BashWebExplanatoryInstance bashx = new("multis")
                {
                    API = new WebServerApi(),
                    Certificate = new X509Certificate2("multis.pfx", "multis")
                };
                await proxy.AddInstance(ws);
                var proxytask = proxy.Run();
                while(Console.ReadKey(true).KeyChar != 'q')
                {
                    await proxy.AddInstance(bashx);
                }
                Console.WriteLine("Stopping..");
                await proxy.Shutdown();
                await proxytask;
            }

            private class OwnWebServer : HttpsHandlerInstance
            {
                public OwnWebServer() : base()
                {
                    StaticFolderPath = "public";
                    Certificate = new X509Certificate2("localhost.pfx", "localhost");
                    AddDomainName("localhost");
                    Start += OnStart;
                    Stop += OnStop;
                }
                protected override async Task ProcessRequestAsync(HttpsListenerContext ctx)
                {
                    Console.WriteLine("Process is called");
                    if (ctx.Request == null) return;
                    string filepath = ctx.Request.Path;
                    Console.WriteLine(filepath);
                    if (filepath.EndsWith('/'))
                    {
                        Console.WriteLine("Does end with '/'");
                        if (File.Exists(filepath+"index.php")) filepath += "index.php";
                        else if (File.Exists(filepath+"index.html")) filepath += "index.html";
                        else await NotFound();
                        Console.WriteLine(filepath);
                    }
                    if (File.Exists(filepath))
                    {
                        await ctx.Response!.SendFile(filepath);
                    }
                    else
                    {
                        await NotFound();
                    }
                }

                private void OnStart(HttpsHandlerInstance instance)
                {
                    Console.WriteLine("OwnWebServer started.");
                }

                private void OnStop(HttpsHandlerInstance instance)
                {
                    Console.WriteLine("OwnWebServer stopped.");
                }
            }

            private class WebServerApi : API
            {
                [EndPointPath("/hello/{user}/{id}", true)]
                public static async Task<(string Content, string MimeType)> Index(HttpsListenerContext ctx, string user, string id)
                {
                    return await Task.FromResult(($"Hello {ctx.Connection.Client.RemoteEndPoint} this is the API index.\n\n{user} : {id}", "text/plain"));
                }
            }
        }
    }
}


#region Testing (Program is Proxy with HttpsHandlerInstance [plus noSSL])


//public static Dictionary<string, Multis.Web.HttpsHandlerInstance> WebServerInstances = [];


//HttpsListener listener = new("localhost", "localhost.pfx", "localhost");
////HttpsListener noSSL = new(Https:false);
////MultisWebServerInstance lol = new("localhost");
////WebServerInstances.AddInstance(lol.DomainNames, lol);
////listener.InvokeStart();
//MultisHttpsHandlerInstance lol = new("localhost");
//WebServerInstances.AddInstance(lol.DomainNames, lol);
//listener.InvokeStart();
//Console.WriteLine("Started Listener");
////Proxy logic
//while (true)
//{
//    try
//    {
//        var ctx = await listener.GetContextAsync();
//        if (ctx.Request == null) continue;
//        _ = Task.Run(async () => await ProxyHandle(ctx));
//    }
//    catch (Exception e)
//    {
//        Console.WriteLine($"Exception occured at {DateTime.Now} with Exception: {e} Message:{e.Message}\n\nData:\n\t{e.Data}");
//        continue;
//    }
//}

//private static async Task ProxyHandle(HttpsListenerContext ctx)
//{
//    string host = ctx.Request!.Host;
//    if(WebServerInstances.TryGetValue(host, out HttpsHandlerInstance? instance))
//    {
//        await instance.HandleConnection(ctx);
//    }
//    else
//    {
//        await ctx.Response!.SendError(404);
//    }
//}
#endregion


#region Testing (Program is Proxy and Instance Logic)

//HttpsListener listener = new("localhost", "localhost.pfx", "localhost");
//listener.AddCertificate("multis", "multis.pfx", "multis");
//listener.InvokeStart();
//Console.WriteLine("Listening on https://localhost:443");
//string staticlolcat = "lolcat_public";
//string staticmultis = "multis_public";
//while (true)
//{
//    try
//    {
//        var ctx = await listener.GetContextAsync();
//        _ = Task.Run(async () =>
//        {
//            try
//            {
//                Console.WriteLine($"Connection found at: {ctx.Connection.Client.RemoteEndPoint?.ToString()?.Split(':')[0]}");
//                string path = ctx.Request.LocalPath;
//                string staticfolder = ctx.Request.Host == "multis" ? staticmultis : staticlolcat;
//                string req = path == "/" ? File.Exists(staticfolder + "/index.html") ? "/index.html" : "/index.php" : path;
//                await ctx.Response.SendFile(staticfolder + req);
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine($"Exception occured at {DateTime.Now} with Exception: {e} Message:{e.Message}\n\nData:\n\t{e.Data}");
//            }
//        });
//    }
//    catch (Exception e)
//    {
//        _ = Task.Run(() => { Console.WriteLine($"Exception occured at {DateTime.Now} with Exception: {e} Message:{e.Message}\n\nData:\n\t{e.Data}"); });
//    }
//}

#endregion


#region Testing (Program is Proxy and Instance with API Logic)

//static HttpsListener listener = new("localhost", "localhost.pfx", "localhost");
//static MultisAPI api = [];
//public async static Task Main(string[] args)
//{
//    listener.AddCertificate("multis", "multis.pfx", "multis");
//    listener.InvokeStart();
//    Console.WriteLine("Listening on https://localhost:443");
//    while (true)
//    {
//        try
//        {
//            var ctx = await listener.GetContextAsync();
//            _ = Task.Run(() => ProcessConnection(ctx));
//        }
//        catch (Exception e)
//        {
//            _ = Task.Run(() => { Console.WriteLine($"Exception occured at {DateTime.Now} with Exception: {e} Message:{e.Message}\n\nData:\n\t{e.Data}"); });
//        }
//    }
//}

//public static async Task ProcessConnection(HttpsListenerContext ctx)
//{
//    try
//    {
//        await api.Invoke(ctx, ctx.Request!.LocalPath);
//    }
//    catch (Exception e)
//    {
//        Console.WriteLine($"Exception occured at {DateTime.Now} with Exception: {e} Message:{e.Message}\n\nData:\n\t{e.Data}");
//        await ctx.Response!.Send("Internal Server Error", "text/plain");
//    }
//}

#endregion