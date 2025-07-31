using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Https
{
    public class HttpsListenerRequest
    {
        private readonly TcpClient? _connection;
        public bool KeepAlive { get; set; } = true;
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = string.Empty;
        public string QueryString { get; set; } = string.Empty;
        public Dictionary<string, string> QueryParameters { get; set; } = [];
        public string Protocol { get; set; } = "HTTP/1.1";
        public Dictionary<string, List<string>> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Host { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string? Body { get; set; }
        private bool _canUseWebSockets { get; set; }

        private bool _isUpgradeRequest { get; set; } = false;
        public bool IsUpgradeRequest => _isUpgradeRequest;

        private string _upgradeRequestHeader { get; set; } = string.Empty;
        public string UpgradeRequestHeader => _upgradeRequestHeader;

        public Stream InputStream { get; set; }
        public CookieCollection Cookies { get; set; } = [];

        public HttpsListenerRequest(TcpClient c, Stream s, HttpsHandlerInstance? i = null)
        {
            _connection = c;
            InputStream = s;
            _canUseWebSockets = i is not null && i.CanUseWebSockets;
        }

        public HttpsListenerRequest(Stream s)
        {
            InputStream = s;
        }

        public async Task ParseRequestAsync(CancellationToken ct = new())
        {
            Console.WriteLine("Parsing Request..");
            using StreamReader sr = new(InputStream, Encoding.UTF8, leaveOpen: true);
            string? requestLine = null;
            try
            {
                requestLine = await Task.Run(sr.ReadLine, ct);
            } catch (TaskCanceledException)
            {
                Console.WriteLine("Request parsing was canceled.");
                return;
            }
            if (string.IsNullOrEmpty(requestLine)) throw new Exception("Invalid HTTP request");

            var parts = requestLine.Split(' ', 3);
            if (parts.Length != 3)
            {
                Console.WriteLine($"Invalid request line: {requestLine}");
                throw new Exception("Malformed request line");
            }

            Method = parts[0].ToUpper();
            Path = parts[1].Split('?')[0];

            if (parts[1].Contains('?'))
            {
                QueryString = parts[1].Split('?')[1];
                foreach (var param in QueryString.Split('&', StringSplitOptions.TrimEntries))
                {
                    var keyValue = param.Split('=', 2);
                    if (keyValue.Length == 2)
                    {
                        QueryParameters[keyValue[0]] = keyValue[1];
                        Console.WriteLine($"Key: {keyValue[0]} | Value: {keyValue[1]}");
                    }
                }
            }

            

            Protocol = parts[2];

            string? line;
            while (!string.IsNullOrEmpty(line = await sr.ReadLineAsync(ct)))
            {
                var headerParts = line.Split(": ", 2, StringSplitOptions.None);
                if (headerParts.Length == 2)
                {
                    if (headerParts[0].Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseCookies(headerParts[1]);
                    }
                    Headers.TryGetValue(headerParts[0], out var existingValues);
                    (existingValues ??= []).Add(headerParts[1]);
                    Headers[headerParts[0]] = existingValues;
                }
            }

            Host = Headers.GetValueOrDefault("Host")?.FirstOrDefault() ?? string.Empty;
            UserAgent = Headers.GetValueOrDefault("User-Agent")?.FirstOrDefault() ?? string.Empty;

            if (Headers.TryGetValue("Connection", out var connectionValues) &&
                connectionValues.Any(value => value.Equals("close", StringComparison.OrdinalIgnoreCase)))
            {
                KeepAlive = false;
            }

            

            if (Headers.TryGetValue("Expect", out var expectValues) &&
                expectValues.Any(value => value.Equals("100-continue", StringComparison.OrdinalIgnoreCase)))
            {
                var writer = new StreamWriter(InputStream) { AutoFlush = true };
                await writer.WriteLineAsync("HTTP/1.1 100 Continue\r\n");
            }

            if (Headers.TryGetValue("Content-Length", out var contentLengthValues) &&
                int.TryParse(contentLengthValues.FirstOrDefault(), out var contentLength))
            {
                char[] buffer = new char[contentLength];
                await sr.ReadAsync(buffer, 0, contentLength);
                Body = new string(buffer);
            }

            //if (Method != "POST")
            //{

            //}

            if (Headers.TryGetValue("Upgrade", out var upgradeValues) &&
                upgradeValues.Any(value => value.Equals("websocket", StringComparison.OrdinalIgnoreCase)))
            {
                //_isUpgradeRequest = true;
                //_upgradeRequestHeader = Headers.GetValueOrDefault("Sec-WebSocket-Key")?.FirstOrDefault() ?? string.Empty;
                //KeepAlive = false;
                //add class for Websocket Connection, the class is here parsed from the strings given from the request, e.g. Sec-WebSocket-Key and so on.
            }


            Console.WriteLine("Request Parsed.");
        }

        private void ParseCookies(string cookieHeader)
        {
            string[] cookies = cookieHeader.Split(';', StringSplitOptions.TrimEntries);
            foreach (string cookie in cookies)
            {
                int separatorIndex = cookie.IndexOf('=');
                if (separatorIndex > 0)
                {
                    string key = cookie[..separatorIndex].Trim();
                    string value = cookie[(separatorIndex + 1)..].Trim().Trim('"');
                    Cookies.Add(new Cookie(key, value));
                }
            }
        }
    }
}
