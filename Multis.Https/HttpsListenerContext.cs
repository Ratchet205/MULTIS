using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Https
{
    public class HttpsListenerContext(HttpsListener parent, TcpClient client)
    {
        private readonly bool _isHttps = parent.IsHttps;
        public bool IsHttps => _isHttps;

        public Dictionary<string, X509Certificate2> Certificates { get; private set; } = parent.Certificates;
        private readonly X509Certificate2? DefaultCertificate = parent.DefaultCertificate;

        public HttpsListenerRequest? Request { get; set; }
        public HttpsListenerResponse? Response { get; set; }

        public HttpsListener Parent { get; set; } = parent;
        public TcpClient Connection { get; set; } = client;
        public HttpsHandlerInstance? Instance { get; set; }

        private readonly CancellationTokenSource cts = new();

        public delegate void HttpsListenerContextClose(HttpsListenerContext sender);
        public event HttpsListenerContextClose? Close;

        private const int MaxTimeoutInSeconds = 4;
        private int timeoutstamp;

        private X509Certificate2 ServerCertificateSelectionCallback(object sender, string? hostName)
        {
            if (!string.IsNullOrEmpty(hostName) && Certificates.TryGetValue(hostName, out X509Certificate2? cert))
            {
                Console.WriteLine($"Using certificate for: {hostName}");
                return cert;
            }

            Console.WriteLine($"No specific certificate for {hostName}, using default.");
            return DefaultCertificate!;
        }

        public async Task Create()
        {
            await Task.Run(() =>
            {
                NetworkStream ns = Connection.GetStream();
                Stream s = ns;
                if (IsHttps)
                {
                    SslStream ssls = new(ns, false);
                    try
                    {
                        var options = new SslServerAuthenticationOptions
                        {
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            ClientCertificateRequired = false,
                            ServerCertificateSelectionCallback = ServerCertificateSelectionCallback
                        };
                        Console.WriteLine("Created Auth-Options");
                        ssls.AuthenticateAsServer(options);
                        s = ssls; // Assign decrypted stream
                        Console.WriteLine("ssls assigned to s");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"TLS Handshake Error: {ex.Message}");
                        Connection.Close(); // 🔴 Close connection if TLS fails
                        throw; // 🔴 Prevent further execution
                    }
                }
                Request = new(Connection, s, Instance);
                Response = new(this, Connection, s);
            });
            if (Request == null) throw new Exception("Unable to create Request");
            await Request.ParseRequestAsync();
        }

        public async Task AliveTillClose()
        {
            await UntilCloseParse(cts.Token);
        }

        private async Task UntilCloseParse(CancellationToken ct)
        {
            async Task parseTask()
            {
                while (Request!.KeepAlive && Connection.Connected && !ct.IsCancellationRequested)
                {
                    Console.WriteLine("Starting to parse request..");
                    await Request!.ParseRequestAsync(cts.Token);
                    Console.WriteLine("Parsed request.. in AliveTillClose");

                    Console.WriteLine("Awaiting instances HandleConnection method..");
                    await Instance!.HandleConnection(this);
                    Console.WriteLine("executed Handle Connection in Still Alive \n\n");

                    ResetTimeout(); // 🟢 Reset timeout after successful handle
                }
            }
            _ = parseTask();

            async Task timertask(CancellationToken token)
            {
                Console.WriteLine("Starting timeout task..");
                timeoutstamp = MaxTimeoutInSeconds;
                while (timeoutstamp > 0)
                {
                    await Task.Delay(1000, token);
                    timeoutstamp--;
                }
                Console.WriteLine("Timeout reached, closing connection.");
                cts.Cancel();
                Console.WriteLine($"Token is {cts.IsCancellationRequested}");
                Close?.Invoke(this);
                Console.WriteLine("Awaiting InvokeConnectionClosed");
                await Instance!.InvokeConnectionClosed(Instance, Connection);
                Console.WriteLine("Invoked.");
                Connection.Close();
                Connection.Dispose();
            }
            await timertask(ct);
            Console.WriteLine("Timer finished");
        }

        private void ResetTimeout()
        {
            timeoutstamp = MaxTimeoutInSeconds;
        }


        public WebSocketConnection UpgradeToWebSocket()
        {
            return new WebSocketConnection(this);
        }
    }
}
