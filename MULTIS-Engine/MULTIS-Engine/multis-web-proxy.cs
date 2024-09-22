using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using HeyRed.Mime;
using System.Reflection;

namespace MULTIS_Engine
{
    public class MultiWebProxy : IDisposable
    {
        private static readonly Dictionary<string, MultiWebProxy> _web_servers = [];
        private static HttpListener? _listener;
        private static bool setup = false;

        private readonly string _domain;
        private readonly string[] prefixes;
        private readonly string staticfolder;
        private readonly Dictionary<string, string> routes = [];

        public string Domain { get { return _domain; } }
        public string[] Prefixes { get { return prefixes; } }
        public string StaticFolder { get { return staticfolder; } }
        public Dictionary<string, string> Routes { get { return routes; } }


        public static Dictionary<string, MultiWebProxy> Web_Servers { get { return _web_servers; } }
        public static HttpListener? Listener { get { return _listener; } }


        public static void CreateListener()
        {
            _listener = new();
        }

        public static void SetUp()
        {
            if(_listener != null)
            {
                ReadWebConfig($"{Worker.WorkerPath}config/web-servers");
                foreach(var server in _web_servers)
                {
                    foreach(var prefix in server.Value.Prefixes)
                    {
                        if(!_listener.Prefixes.Contains(prefix))
                        {
                            _listener.Prefixes.Add(prefix);
                        }
                    }
                }
                setup = true;
                return;
            }
            throw new Exception("CreateListener must be called first.");
        }

        public static void SetUp(ILogger<Worker> logger)
        {
            if (_listener != null)
            {
                ReadWebConfig(logger, $"{Worker.WorkerPath}config/web-servers");
                foreach (var server in _web_servers)
                {
                    foreach (var prefix in server.Value.Prefixes)
                    {
                        if (!_listener.Prefixes.Contains(prefix))
                        {
                            logger.LogInformation("[{ServerName}] adding Prefix: {Prefix}", server.Value.Domain, prefix);
                            _listener.Prefixes.Add(prefix);
                        }
                    }
                }
                setup = true;
                return;
            }
            throw new Exception("CreateListener must be called first.");
        }

        public static void SetUp(string extraConfig)
        {
            if (_listener != null)
            {
                ReadWebConfig(extraConfig);
                foreach (var server in _web_servers)
                {
                    foreach (var prefix in server.Value.Prefixes)
                    {
                        if (!_listener.Prefixes.Contains(prefix))
                        {
                            _listener.Prefixes.Add(prefix);
                        }
                    }
                }
                setup = true;
                return;
            }
            throw new Exception("CreateListener must be called first.");
        }

        public static void Start()
        {
            if(setup && _listener != null)
            {
                _listener.Start();
                return;
            }
            throw new Exception("Server hasn't been Set Up yet. Call SetUp() first.");
        }

        public static void ReadWebConfig(string? workerpath)
        {
            var EFiles = Directory.EnumerateFiles($"{workerpath}");
            foreach (var EFile in EFiles)
            {
                string json_string = File.ReadAllText(EFile);
                var config = JObject.Parse(json_string);
                MultiWebProxy _ = new(config);
            }
        }

        public static void ReadWebConfig(ILogger<Worker> logger, string? workerpath)
        {
            var EFiles = Directory.EnumerateFiles($"{workerpath}");
            foreach (var EFile in EFiles)
            {
                string json_string = File.ReadAllText(EFile);
                var config = JObject.Parse(json_string);
                MultiWebProxy _ = new(logger, config);
            }
        }

        public void Dispose()
        {
            // Perform cleanup of managed resources
            Dispose(true);

            // Suppress finalization since we've already cleaned up
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Free any other managed objects here.
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                    _listener = null;
                }

                _web_servers.Clear();
            }

            // Free any unmanaged objects here.
        }


        public MultiWebProxy(JObject config)
        {
            // Find the first top-level object in the config JSON
            var firstProperty = config.Properties().FirstOrDefault();

            _domain = firstProperty?.Name ?? "Unknown";

            if (firstProperty == null || firstProperty.Value is not JObject mainConfig)
            {
                throw new ArgumentException("Invalid config format. The configuration section is missing or malformed.");
            }

            staticfolder = mainConfig["staticfolder"]?.ToString() ?? "public";
            prefixes = mainConfig["prefixes"]?.ToObject<string[]>() ?? []; // Initialize to empty array if null

            if (prefixes.Length == 0)
            {
                throw new ArgumentException("Prefixes array cannot be empty.");
            }

            _domain = prefixes[0];

            var Jroutes = mainConfig["routes"]?.ToObject<JObject>();
            if (Jroutes != null)
            {
                foreach (var route in Jroutes)
                {
                    routes[route.Key] = route.Value?.ToString() ?? string.Empty;
                }
            }

            // Store the server instance in the dictionary
            _web_servers[_domain] = this;
        }

        public MultiWebProxy(ILogger<Worker> logger, JObject config)
        {
            // Find the first top-level object in the config JSON
            var firstProperty = config.Properties().FirstOrDefault();

            _domain = firstProperty?.Name ?? "Unknown";

            if (firstProperty == null || firstProperty.Value is not JObject mainConfig)
            {
                throw new ArgumentException("Invalid config format. The configuration section is missing or malformed.");
            }

            staticfolder = mainConfig["staticfolder"]?.ToString() ?? "public";
            prefixes = mainConfig["prefixes"]?.ToObject<string[]>() ?? []; // Initialize to empty array if null

            if (prefixes.Length == 0)
            {
                throw new ArgumentException("Prefixes array cannot be empty.");
            }

            var Jroutes = mainConfig["routes"]?.ToObject<JObject>();
            if (Jroutes != null)
            {
                foreach (var route in Jroutes)
                {
                    routes[route.Key] = route.Value?.ToString() ?? string.Empty;
                }
            }

            // Store the server instance in the dictionary
            logger.LogInformation("Server {name} loaded successfully.", _domain);
            _web_servers[_domain] = this;
        }

        public async Task Responde(HttpListenerRequest req, HttpListenerResponse res, CancellationToken cts)
        {
            string reqPath = req.RawUrl ?? "/";
            try
            {
                byte[] buffer;
                string resPath = routes.TryGetValue(reqPath, out var path)
                    ? path
                    : req.RawUrl == "/"
                        ? staticfolder + "/index.html"
                        : staticfolder + req.RawUrl;
                if(File.Exists(resPath))
                {
                    string mimeType = MimeTypesMap.GetMimeType(resPath);
                    using(var fileStream = File.OpenRead(resPath))
                    {
                        buffer = new byte[fileStream.Length];
                        await fileStream.ReadAsync(buffer, cts);
                    }
                    res.ContentType = mimeType;
                } else
                {
                    buffer = Encoding.UTF8.GetBytes("File not Found");
                    res.StatusCode = 404;
                    res.ContentType = "text/plain";
                }

                res.ContentLength64 = buffer.Length;
                using var output = res.OutputStream;
                await output.WriteAsync(buffer, cts);
            } catch
            {
                res.StatusCode = 500;
            } finally
            {
                res.OutputStream.Close();
            }
        }

        public async Task Responde(ILogger<Worker> logger, HttpListenerRequest req, HttpListenerResponse res, CancellationToken cts)
        {
            string reqPath = req.RawUrl ?? "/";
            byte[] buffer;

            try
            {
                string resPath = routes.TryGetValue(reqPath, out var path)
                    ? Path.Combine(staticfolder, path)
                    : reqPath == "/"
                        ? Path.Combine(staticfolder, "index.html")
                        : Path.Combine(staticfolder, reqPath.TrimStart('/'));

                if (File.Exists(resPath))
                {
                    string mimeType = MimeTypesMap.GetMimeType(resPath);
                    res.ContentType = mimeType;

                    using var fileStream = File.OpenRead(resPath);
                    buffer = new byte[fileStream.Length];
                    await fileStream.ReadAsync(buffer, cts);
                }
                else
                {
                    logger.LogInformation("File \"{reqPath}\" not found", reqPath);
                    buffer = Encoding.UTF8.GetBytes($"\"{reqPath}\" not found");
                    res.StatusCode = 404;
                    res.ContentType = "text/plain";
                }

                res.ContentLength64 = buffer.Length;

                // Write to OutputStream
                using var output = res.OutputStream;
                await output.WriteAsync(buffer, cts);
            }
            catch (Exception ex)
            {
                logger.LogError("Error processing request: {Message}", ex.Message);
                res.StatusCode = 500;
                byte[] errorBuffer = Encoding.UTF8.GetBytes("Internal Server Error");

                // Ensure output stream is available before writing
                try
                {
                    using var output = res.OutputStream;
                    await output.WriteAsync(errorBuffer, cts);
                }
                catch (ObjectDisposedException objEx)
                {
                    logger.LogDebug("OutputStream already disposed: {Message}", objEx.Message);
                }
                catch (Exception writeEx)
                {
                    logger.LogError("Error writing to response: {Message}", writeEx.Message);
                }
            }
            finally
            {
                // No need to close OutputStream here, as it's handled in the using statement
            }
        }


    }
}
