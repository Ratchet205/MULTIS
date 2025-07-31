using Multis.Https;
using Multis.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Multis.App
{
    public class BashWebExplanatoryInstance : HttpsHandlerInstance
    {
        private static readonly string apefPattern = "*.apef";
        public BashWebExplanatoryInstance(string? _domainName = null) : base(_domainName)
        {
            Start += OnStart;
            Stop += OnStop;
            ConnectionReceived += OnConnectionReceived;
            ConnectionClosed += OnConnectionClosed;
            StaticFolderPath = "public";
        }
        protected override async Task ProcessRequestAsync(HttpsListenerContext ctx)
        {
            await ctx.Response!.SendFile(ctx.Request!.Path);
        }

        public static void OnStart(HttpsHandlerInstance instance)
        {
            try
            {
                string[] files = Directory.GetFiles("./", apefPattern);

                if (files.Length == 0)
                {
                    Console.WriteLine("No .apef files found.");
                    return;
                }

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        Console.WriteLine($"Deleted file: {file}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error deleting file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error matching files: {ex.Message}");
            }
        }

        public static void OnStop(object o)
        {
            Console.WriteLine("Hat gestoppt.");
        }

        public static async void OnConnectionReceived(HttpsHandlerInstance instance, HttpsHandlerInstanceConnectionEventArgs args)
        {
            string filename = $"{args.EndPoint!.ToString()!.Split(':')[0]}.apef";
            if(!File.Exists(filename))
            {
                Console.WriteLine($"{filename} existiert nicht.");
                File.Create(filename).Close();
                File.WriteAllText(filename, "1");
                Console.WriteLine($"{filename} erstellt.");
            }
            else
            {
                Console.WriteLine($"{filename} existiert.");
                Console.WriteLine($"Lese {filename}.");
                string tabcount = await File.ReadAllTextAsync(filename);
                Console.WriteLine($"{filename} gelesen: {tabcount}");
                int count = int.Parse(tabcount);
                Console.WriteLine($"Parsing Tabcount");
                Console.WriteLine($"Erhöhe {filename} um 1.");
                count++;
                Console.WriteLine($"Schreibe {filename}.");
                await File.WriteAllTextAsync(filename, count.ToString());
                Console.WriteLine($"Schreibe {filename} fertig.");
                Console.WriteLine($"{args.EndPoint!.ToString()!.Split(':')[0]} hat {count} Tabs geöffnet.");
            }

        }

        public static async void OnConnectionClosed(HttpsHandlerInstance instance, HttpsHandlerInstanceConnectionEventArgs args)
        {
            string filename = $"{args.EndPoint!.ToString()!.Split(':')[0]}.apef";
            if(File.Exists(filename))
            {
                string tabcount = await File.ReadAllTextAsync(filename);
                int count = int.Parse(tabcount);
                count--;
                if (!(count > 0))
                {
                    File.Delete(filename);
                    Console.WriteLine($"File {filename} deleted");
                    return;
                }
                await File.WriteAllTextAsync(filename, count.ToString());
            }
        }
    }
}
