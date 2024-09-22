using System;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using HeyRed.Mime;
using System.Net;
using System.Text;

namespace MULTIS_Engine
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private const string dummyjson = @"{""dummy"":{""staticfolder"":""/srv/dummy/public"",""prefixes"":[""http:/localhost:8080/""],""routes"":{""/login"":""html/login.html"",""/logout"":""html/logout.html""}}}";

        private Timer? _timer;

        private readonly static string workerpath = OperatingSystem.IsLinux() ? "/etc/multis/" : string.Empty;
        public static string WorkerPath { get { return workerpath; } }


        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Ensure directories exist
            EnsureDirectoryExists(workerpath);
            EnsureDirectoryExists($"{workerpath}config");
            EnsureDirectoryExists($"{workerpath}config/proxy-config");
            EnsureDirectoryExists($"{workerpath}config/web-servers");
            EnsureDirectoryExists($"{workerpath}debug");
            EnsureDirectoryExists($"{workerpath}debug/proxy-config");
            EnsureDirectoryExists($"{workerpath}debug/web-servers");

            // Write dummy JSON if it doesn't exist
            if (!File.Exists($"{workerpath}debug/web-servers/dummy.json"))
            {
                File.WriteAllText($"{workerpath}debug/web-servers/dummy.json", dummyjson);
            }

            // Create listener and setup web proxy
            MultiWebProxy.CreateListener();
            MultiWebProxy.SetUp(_logger);
            MultiWebProxy.Start();

            _logger.LogInformation("Listener is Running...");

            // Set up the timer to scan for new files every 10 seconds
            _timer = new Timer(CheckForNewJsonFiles, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));

            return base.StartAsync(cancellationToken);
        }

        private bool EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path) && !string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("Created directory at: {path}", path);
                return false;
            }
            return true;
        }

        // Method to check for new JSON files
        private void CheckForNewJsonFiles(object? state)
        {
            _logger.LogInformation("Checking for new JSON configuration files...");
            string configPath = $"{workerpath}config/web-servers";

            try
            {
                MultiWebProxy.ReadWebConfig(_logger, configPath);
                _logger.LogInformation("Configuration reloaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while reloading configuration: {Message}", ex.Message);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (MultiWebProxy.Listener != null)
                    {
                        HttpListenerContext context = await MultiWebProxy.Listener.GetContextAsync();
                        _logger.LogInformation("Context gained from: {Name}, at {Url}", context.User?.Identity?.Name ?? "Unknown", context.Request.Url);

                        if (context != null)
                        {
                            var req = context.Request;
                            var res = context.Response;

                            string baseurl = req.Url?.GetLeftPart(UriPartial.Authority) + "/";
                            _logger.LogInformation("Request for \"{RawUrl}\" at {baseurl}", req.RawUrl, baseurl);

                            // Safely get the server instance and respond
                            if (MultiWebProxy.Web_Servers.TryGetValue(baseurl, out var server))
                            {
                                await server.Responde(_logger, req, res, stoppingToken);
                            }
                            else
                            {
                                _logger.LogWarning("No server found for URL: {Url}", req.Url);

                                // Set response to 404 and send "Server Not Found" message
                                res.StatusCode = 404;

                                try
                                {
                                    if (!res.OutputStream.CanWrite)
                                    {
                                        _logger.LogWarning("Cannot write to the response stream for URL: {Url}", req.Url);
                                    }
                                    else
                                    {
                                        byte[] buffer = Encoding.UTF8.GetBytes("Server Not Found");
                                        await res.OutputStream.WriteAsync(buffer, stoppingToken);
                                        await res.OutputStream.FlushAsync(stoppingToken);
                                    }
                                }
                                catch (ObjectDisposedException ex)
                                {
                                    _logger.LogDebug("Client disconnected or HttpListenerResponse was already disposed: {Message}", ex.Message);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("Unexpected error while writing response: {Message}", ex.Message);
                                }
                                finally
                                {
                                    try
                                    {
                                        res.Close(); // Ensure response is closed properly
                                    }
                                    catch (ObjectDisposedException)
                                    {
                                        _logger.LogDebug("Response already disposed.");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning("Error while closing HttpListenerResponse: {Message}", ex.Message);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (HttpListenerException ex) when (ex.Message.Contains("The operation was canceled"))
                {
                    _logger.LogInformation("Server stopped listening for new connections.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unhandled exception ({ex}) in ExecuteAsync: {Message}", ex, ex.Message);
                }
            }
        }


        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            return base.StopAsync(cancellationToken);
        }
    }
}
