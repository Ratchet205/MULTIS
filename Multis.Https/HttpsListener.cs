using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Multis.Https
{
    public class HttpsListener
    {
        #region keep-alive connection handling
        #endregion
        private bool _listening = false;
        public bool IsListening => _listening;
        private Dictionary<string, X509Certificate2> _certificates = [];
        public Dictionary<string, X509Certificate2> Certificates
        {
            get => _certificates;
            set
            {
                _certificates = value;
                IsHttps = value != null;
            }
        }

        private X509Certificate2? _defaultCertificate;

        public X509Certificate2? DefaultCertificate
        {
            get => _defaultCertificate;
            private set
            {
                _defaultCertificate = value;
                IsHttps = value != null;
            }
        }

        private bool _isHttps = false;
        public bool IsHttps
        {
            get => _isHttps;
            private set
            {
                _isHttps = value;
                if (value) _listener = new(IPAddress.Any, 443);
                else _listener = new(IPAddress.Any, 80);
            }
        }


        private TcpListener _listener = new(IPAddress.Any, 443);
        
        
        
        public async Task<HttpsListenerContext> GetContextAsync(CancellationToken ct)
        {
            if (!_listening) throw new InvalidOperationException("Listener isn't running. Call Start() first.");
            var con = await _listener.AcceptTcpClientAsync(ct);
            Console.WriteLine($"[{DateTime.Now}] Connection recived from {con.Client.RemoteEndPoint}");
            var ctx = new HttpsListenerContext(this, con);
            await ctx.Create();
            return ctx;
        }

        public void Start()
        {
            _listener.Start();
            _listening = true;
            Console.WriteLine($"Listening on {_listener.LocalEndpoint}");
        }

        public void Stop()
        {
            _listener.Stop();
            _listening = false;
        }

        public void Close()
        {
            if (_listening) Stop();
            _listener.Dispose();
        }

        public void AddCertificate(string hostname, string certFilePath, string? passwd = null)
        {
            if (!IsValidDomainNameOrIP(hostname)) throw new ArgumentException("Invalid domain name.");
            if (!IsHttps) IsHttps = true;
            var Certificate = new X509Certificate2(certFilePath, passwd);
            DefaultCertificate ??= Certificate;
            _certificates[hostname] = Certificate;
            Console.WriteLine($"Added certificate for {hostname}");
        }

        public void AddCertificate(string hostname, X509Certificate2 certificate)
        {
            if (!IsValidDomainNameOrIP(hostname)) throw new ArgumentException("Invalid domain name.");
            if (!IsHttps) IsHttps = true;
            _certificates[hostname] = certificate;
            DefaultCertificate ??= certificate;
            Console.WriteLine($"Added certificate for {hostname}");
        }

        public void RemoveCertificate(string hostname)
        {
            _certificates.Remove(hostname);
            if (_certificates.Count == 0)
            {
                IsHttps = false;
                DefaultCertificate = null;
            }
            Console.WriteLine($"Removed certificate for {hostname}");
        }

        public HttpsListener(string hostname, string certFilePath, string? passwd = null)
        {
            if (!IsValidDomainNameOrIP(hostname)) throw new ArgumentException("Invalid domain name.");
            var Certificate = new X509Certificate2(certFilePath, passwd);
            DefaultCertificate = Certificate;
            _certificates[hostname] = DefaultCertificate;
            
        }

        public HttpsListener(string hostname, X509Certificate2 certificate)
        {
            if (!IsValidDomainNameOrIP(hostname)) throw new ArgumentException("Invalid domain name.");
            DefaultCertificate = certificate;
            _certificates[hostname] = DefaultCertificate;
            
        }

        public HttpsListener(TcpListener? l)
        {
            if(l != null) _listener = l;
            
        }

        public HttpsListener(IPAddress? ip = null, int? port = null, bool Https = false)
        {
            IsHttps = Https;
            _listener = new(ip ?? IPAddress.Any, port == null ? IsHttps ? 433 : 80 : (int)port);
        }

        public static bool IsValidDomainNameOrIP(string input)
        {
            string domainPattern = @"^(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.[A-Za-z]{2,6})?$";
            string ipPattern = @"^(\d{1,3}\.){3}\d{1,3}$";

            return Regex.IsMatch(input, domainPattern) || Regex.IsMatch(input, ipPattern);
        }

    }
}
