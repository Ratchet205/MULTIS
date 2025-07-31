using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MimeMapping;

namespace Multis.Https
{
    public class HttpsListenerResponse
    {
        private readonly HttpsListenerContext? _parentContext;
        private readonly TcpClient? _connection;
        public Stream OutputStream { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public bool KeepAlive { get; set; } = true;
        public string Charset { get; set; } = "UTF-8";
        public string ContentType { get; set; } = "text/plain";
        public Encoding ContentEncoding { get; set; } = Encoding.UTF8;
        public long ContentLength64 { get; set; } = 0;
        public string Protocol { get; set; } = "HTTP/1.1";
        private readonly StringBuilder _sb = new();

        public CookieCollection Cookies { get; set; } = [];
        public Dictionary<string, List<string>> Headers { get; set; } = [];

        private async Task SendHeaders(string? statusMessage = null)
        {
            // Write status line
            string message = statusMessage ?? StatusCode.ToString();
            byte[] headline = Encoding.UTF8.GetBytes($"{Protocol} {(int)StatusCode} {message}\r\n");
            await OutputStream.WriteAsync(headline);

            // Add the Content-Length header if it's not chunked
            if (!Headers.TryGetValue("Transfer-Encoding", out List<string>? value) || !value.Contains("chunked"))
            {
                byte[] contentLengthHeader = Encoding.UTF8.GetBytes($"Content-Length: {ContentLength64}\r\n");
                await OutputStream.WriteAsync(contentLengthHeader);
            }

            if(!Headers.ContainsKey("Content-Type"))
            {
                await OutputStream.WriteAsync(Encoding.UTF8.GetBytes($"Content-Type: {ContentType}; charset={Charset}\r\n"));
            }

            // Add cookies to headers
            foreach (Cookie cookie in Cookies)
            {
                string cookieHeader = $"Set-Cookie: {cookie.Name}={cookie.Value};";
                if (cookie.Expires != DateTime.MinValue)
                {
                    cookieHeader += $" Expires={cookie.Expires:R};";
                }
                if (cookie.Domain != null)
                {
                    cookieHeader += $" Domain={cookie.Domain};";
                }
                if (cookie.Path != null)
                {
                    cookieHeader += $" LocalPath={cookie.Path};";
                }
                if (cookie.Secure)
                {
                    cookieHeader += " Secure;";
                }
                if (cookie.HttpOnly)
                {
                    cookieHeader += " HttpOnly;";
                }

                byte[] cookieBytes = Encoding.UTF8.GetBytes(cookieHeader + "\r\n");
                try
                {
                    await OutputStream.WriteAsync(cookieBytes);
                } catch (Exception ex)
                {
                    Console.WriteLine($"{ex}, {ex.Message}");
                }
            }

            // Write other headers
            foreach (var pair in Headers)
            {
                foreach (var pairvalue in pair.Value)
                {
                    byte[] headerBytes = Encoding.UTF8.GetBytes($"{pair.Key}: {pairvalue}\r\n");
                    await OutputStream.WriteAsync(headerBytes);
                }
            }

            // Write final blank line indicating end of header section
            await OutputStream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"));
        }

        public async Task WriteAsync(string ResponseContent)
        {
            await Task.Run(() =>
            {
                _sb.Append(ResponseContent);
                ContentLength64 = Encoding.UTF8.GetBytes(_sb.ToString()).Length;
            });
        }

        public async Task WriteAsync(char[] ResponseContent)
        {
            await WriteAsync(new string(ResponseContent));
        }

        public async Task WriteAsync(byte[] ResponseContent)
        {
            await WriteAsync(Encoding.UTF8.GetString(ResponseContent));
        }

        public async Task Send(string content, string mimetype)
        {
            Console.WriteLine($"Begin: {content} | {mimetype}");
            _sb.Clear();
            _sb.Append(content);
            ContentLength64 = Encoding.UTF8.GetBytes(_sb.ToString()).Length;
            ContentType = mimetype;
            await Close();
            Console.WriteLine($"End: {content} | {mimetype}");
        }

        public async Task SendFile(string filepath)
        {
            try
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
                if (filepath.EndsWith('/'))
                {
                    Console.WriteLine("Does end with '/'");
                    if (File.Exists(filepath + "index.php")) filepath += "index.php";
                    else if (File.Exists(filepath + "index.html")) filepath += "index.html";
                    else await _parentContext!.Instance!.NotFound();
                    Console.WriteLine(filepath);
                }
                using FileStream fileStream = new(filepath, FileMode.Open);
                string fileName = Path.GetFileName(fileStream.Name);
                ContentType = MimeUtility.GetMimeMapping(fileName);
                Headers["Content-Type"] = [ContentType];

                // Use Content-Length since the file size is known
                ContentLength64 = fileStream.Length;

                await SendHeaders();

                // Copy file to output stream (faster for small files)
                await fileStream.CopyToAsync(OutputStream);
            } catch
            {
                Console.WriteLine($"\n\n\nFile \"{filepath}\" not Found.\n\n");
                StatusCode = HttpStatusCode.NotFound;
                try
                {
                    var (content, mimetype) = await _parentContext!.Instance!.NotFound();
                    await Send(content, mimetype);
                } catch
                {
                    await SendError(500);
                }
                return;
            }
        }


        public async Task SendFileChunked(FileStream fileStream)
        {
            
            byte[] buffer = new byte[8192]; // 8 KB chunks
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer)) > 0)
            {
                // Write chunk size in hex followed by CRLF
                string chunkSize = $"{bytesRead:X}\r\n";
                byte[] chunkSizeBytes = Encoding.ASCII.GetBytes(chunkSize);
                await OutputStream.WriteAsync(chunkSizeBytes);

                // Write chunk data
                await OutputStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                // End chunk with CRLF
                await OutputStream.WriteAsync(Encoding.ASCII.GetBytes("\r\n").AsMemory(0, 2));
            }

            // Send final chunk (size 0) to signal end
            await OutputStream.WriteAsync(Encoding.ASCII.GetBytes("0\r\n\r\n").AsMemory(0, 5));
        }


        public async Task SendError(HttpStatusCode hsc)
        {
            StatusCode = hsc;
            Headers = [];
            ContentType = "text/plain";
            await SendHeaders("NOT FOUND");
            await OutputStream.FlushAsync();
            _sb.Clear();
            if (KeepAlive) return;
            _connection?.Close();
        }


        public async Task SendError(int hsc)
        {
            await SendError((HttpStatusCode)hsc);
        }


        public void AppendCookie(Cookie c)
        {
            Cookies.Add(c);
        }

        public void SetCookie(Cookie c)
        {
            // You can modify existing cookies here if needed
            Cookies.Add(c);
        }

        public void Abort()
        {
            OutputStream.Dispose();
        }

        public async Task Close()
        {
            await SendHeaders();
            await OutputStream.WriteAsync(Encoding.UTF8.GetBytes(_sb.ToString()));
            await OutputStream.FlushAsync();
            if(KeepAlive)
            {
                _sb.Clear();
                return;
            }
            _connection?.Close();
            _connection?.Dispose();
        }

        public HttpsListenerResponse(HttpsListenerContext? parent, TcpClient c, Stream s)
        {
            if(parent != null)
            {
                _parentContext = parent;
                Headers = parent.Request?.Headers ?? [];
                KeepAlive = parent.Request?.KeepAlive ?? true;
            }
            _connection = c;
            OutputStream = s;
        }

        public HttpsListenerResponse(Stream s)
        {
            OutputStream = s;
        }
    }
}
