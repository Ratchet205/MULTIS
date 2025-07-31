using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multis.Https;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.Runtime.CompilerServices;
using System.Numerics;

namespace Multis.Https
{
    public abstract class HttpsHandlerInstance
    {
        #region Events
        public delegate void HttpsHandlerInstanceEventSignature(HttpsHandlerInstance sender);
        public delegate void HttpsHandlerInstanceConnectionEventSignature(HttpsHandlerInstance sender, HttpsHandlerInstanceConnectionEventArgs args);
        public delegate void HttpsHandlerInstanceResponseRequestEventSignature(HttpsHandlerInstance sender, HttpsListenerContext ctx);

        public event HttpsHandlerInstanceEventSignature? Start;
        public event HttpsHandlerInstanceEventSignature? Stop;
        public event HttpsHandlerInstanceEventSignature? Restart;
        //public event HttpsHandlerInstanceEventSignature? AbortInstance;
        //public event HttpsHandlerInstanceEventSignature? AbortHandle;
        public event HttpsHandlerInstanceResponseRequestEventSignature? RequestReceived;
        public event HttpsHandlerInstanceResponseRequestEventSignature? ResponseSent;
        //public event HttpsHandlerInstanceResponseRequestEventSignature? RequestSent;
        //public event HttpsHandlerInstanceResponseRequestEventSignature? ResponseReceived;
        public event HttpsHandlerInstanceConnectionEventSignature? ConnectionReceived;
        public event HttpsHandlerInstanceConnectionEventSignature? ConnectionClosed;
        #endregion

        private readonly HttpsKeepAliveConnectionList _keepAliveConnections = new();
        protected HttpsHandlerInstance(string? _domainName = null) { if (_domainName != null) AddDomainName(_domainName); }
        protected HttpsHandlerInstance(API? api, string? staticFolderPath)
        {
            API = api;
            StaticFolderPath = staticFolderPath != null ? staticFolderPath.TrimEnd('/', '\\') : "";
        }
        protected HttpsHandlerInstance(string _domainName, string certFilePath, string? passwd = null)
        {
            AddDomainName(_domainName);
            Certificate = new X509Certificate2(certFilePath, passwd);
        }
        public bool UseStaticFolder { get; set; } = true; //by default true.
        public string StaticFolderPath { get; set; } = string.Empty; //_staticfolderpath != null ? _staticfolderpath.TrimEnd('/', '\\') : "";

        private readonly List<string> _domainNames = [];
        public IEnumerable<string> DomainNames => _domainNames;
        public X509Certificate2? Certificate { get; set; } = null;

        private bool _canUseWebSockets = true;
        public bool CanUseWebSockets
        {
            get => _canUseWebSockets;
            set
            {
                _canUseWebSockets = value;
                _autoUpgradeWebSockets = value;
            }
        }

        private bool _autoUpgradeWebSockets = true;
        public bool AutoUpgradeWebSockets
        {
            get => _autoUpgradeWebSockets;
            set
            {
                if(!CanUseWebSockets) throw new InvalidOperationException("WebSockets not supported.");
                _autoUpgradeWebSockets = value;
            }
        }

        public bool Started { get; private set; } = false;

        //private bool _isStandAlone = false;
        //public bool IsStandAlone
        //{
        //    get => _isStandAlone;
        //    set => _isStandAlone = value;
        //}

        private bool _hasApi = false;
        public bool HasApi
        {
            get => _hasApi;
            set
            {
                _hasApi = value;
                if(!value) _api = null;
            }
        }
        private API? _api;
        public API? API
        {
            get => _api;
            set
            {
                if (value == null) _hasApi = false; else _hasApi = true;
                _api = value;
            }
        }

        public virtual async Task HandleConnection(HttpsListenerContext ctx)
        {
            /*
             
                Handling Keep-Alive Connections, creating a new Class containing the HandlerInstance, the HttpsListenerContext and the TcpClient
                and then calling the ParseRequestAsync method of the HttpsListenerContexts Request via await continuously until the header in the request and or response is "close".

             */
            try
            {
                var receivedTask = Task.Run(()=>RequestReceived?.Invoke(this, ctx));
                Action? connectionClosed = null;
                if (ctx.Request!.KeepAlive)
                {
                    Console.WriteLine("is Keep-Alive");
                    if (!_keepAliveConnections.Contains(ctx))
                    {
                        Console.WriteLine("Adding to Keep-Alive Connections");
                        ctx.Instance = this;
                        await _keepAliveConnections.Add(ctx);
                        Console.WriteLine("Added to Keep-Alive Connections");
                    }
                } else
                {
                    connectionClosed = () => { ConnectionClosed?.Invoke(this, ctx.Connection); };
                }
                if (HasApi)
                {
                    await API!.Invoke(ctx);
                    return;
                }
                if(UseStaticFolder)
                {
                    ctx.Request!.Path = StaticFolderPath.TrimEnd('/', '\\') + ctx.Request.Path;
                }
                await ProcessRequestAsync(ctx);
                await receivedTask;
                await Task.Run(() => connectionClosed?.Invoke());
            }
            catch(IsNoAPIEndPointException)
            {
                Console.WriteLine($"{ctx.Request!.Path} is no API Endpoint.");
                if (UseStaticFolder)
                {
                    ctx.Request!.Path = StaticFolderPath.TrimEnd('/', '\\') + ctx.Request.Path;
                    Console.WriteLine(ctx.Request!.Path);
                }
                await ProcessRequestAsync(ctx);
            }
            finally
            {
                if(ctx.Connection.Connected && !ctx.Request!.KeepAlive)
                {
                    (string content, string mimetype) = await NotFound();
                    await ctx.Response!.Send(content, mimetype);
                }
                ResponseSent?.Invoke(this, ctx);
            }
        }

        public async Task InvokeStart()
        {
            await Task.Run(() =>
            {
                if (Started) throw new InvalidOperationException("Already started.");
                Start?.Invoke(this);
                Started = true;
            });
        }

        public async Task InvokeStop()
        {
            await Task.Run(() =>
            {
                if (!Started) throw new InvalidOperationException("Instance not Running.");
                Stop?.Invoke(this);
                Started = false;
            });
        }

        public async Task InvokeRestart()
        {
            await Task.Run(async() =>
            {
                if (!Started) throw new InvalidOperationException("Instance not Running.");
                Restart?.Invoke(this);
                await InvokeStop();
                await InvokeStart();
            });
        }

        public async Task InvokeConnectionRecieved(HttpsHandlerInstance instance, TcpClient c)
        {
            await Task.Run(() =>
            {
                ConnectionReceived?.Invoke(instance, c);
            });
        }

        public async Task InvokeConnectionClosed(HttpsHandlerInstance instance, TcpClient c)
        {
            await Task.Run(() =>
            {
                ConnectionClosed?.Invoke(instance, c);
            });
            Console.WriteLine("Connection Closed.");
        }
        protected abstract Task ProcessRequestAsync(HttpsListenerContext ctx);

        public virtual void AddDomainName(string domainName)
        {
            if (string.IsNullOrEmpty(domainName)) throw new ArgumentException("Domain name cannot be null or empty.");
            if (_domainNames.Contains(domainName)) return;
            _domainNames.Add(domainName);
        }

        public virtual Task<(string content, string mimetype)> NotFound()
        {
            return Task.FromResult(($"Page not Found; 404", "text/plain"));
        }
    }

    //public class HttpsHandlerInstanceNoResultException(string message) : Exception(message) { }
    public class HttpsHandlerInstanceConnectionEventArgs(TcpClient c)
    {
        private readonly EndPoint? _ep = c.Client.RemoteEndPoint;
        public EndPoint? EndPoint => _ep;

        public static implicit operator HttpsHandlerInstanceConnectionEventArgs(TcpClient c)
        {
            return new HttpsHandlerInstanceConnectionEventArgs(c);
        }
    }
}
