using Multis.Https;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Web
{
    public class HttpsHandlerProxy
    {
        #region event argument classes
        protected class HttpsHandlerProxyEventArgs(HttpsListenerContext ctx) : EventArgs
        {
            public HttpsListenerContext Context { get; } = ctx;
        }
        #endregion
        #region event delegates
        public delegate void HttpsHandlerProxyEventSignature(object sender);
        #endregion


        private readonly Dictionary<string, HttpsHandlerInstance> _instances = [];
        public Dictionary<string, HttpsHandlerInstance> Instances => _instances;
        private CancellationTokenSource _cts = new();

        private readonly HttpsListener _listener;
        public HttpsListener Listener => _listener;

        private bool _isRunning = false;
        public bool IsRunning => _isRunning;

        private readonly bool _isHttps = true;
        public bool IsHttps => _isHttps;

        public HttpsHandlerProxy(HttpsListener listener)
        {
            _listener = listener;
        }

        public HttpsHandlerProxy(string hostname, string certFilePath, string? passwd = null)
        {
            _listener = new(hostname, certFilePath, passwd);
        }

        public HttpsHandlerProxy(bool https = true)
        {
            _isHttps = https;
            _listener = new(Https:_isHttps);
        }

        public async Task AddInstance(HttpsHandlerInstance instance)
        {
            foreach(var domainName in instance.DomainNames)
            {
                if(string.IsNullOrEmpty(domainName)) throw new ArgumentException("Domain name cannot be null or empty.");
                if(_instances.ContainsKey(domainName)) throw new ArgumentException($"Instance with domain name {domainName} already exists.");
                _instances.Add(domainName, instance);
                if (!_isHttps) return;
                if (instance.Certificate == null) throw new ArgumentException($"Instance {instance.DomainNames} does not have a certificate.");
                _listener.AddCertificate(domainName, instance.Certificate);
            }
            await instance.InvokeStart();
        }

        public async Task RemoveInstance(string domainName)
        {
            if (!_instances.TryGetValue(domainName, out HttpsHandlerInstance? instance))
                throw new ArgumentException($"Instance with domain name {domainName} does not exist.");
            if(instance == null) throw new ArgumentNullException($"Instance with domain name {domainName} is null.");
            await instance.InvokeStop();
            _instances.Remove(domainName);
        }

        public async Task RemoveInstances(IEnumerable<string> domainNames)
        {
            foreach (var domainName in domainNames)
            {
                await RemoveInstance(domainName);
            }
        }

        public async Task ReplaceOrAddInstance(HttpsHandlerInstance instance)
        {
            foreach(var domainname in instance.DomainNames)
            {
                if (string.IsNullOrEmpty(domainname)) throw new ArgumentException("Domain name cannot be null or empty.");
                if (!_instances.TryAdd(domainname, instance))
                {
                    _instances[domainname] = instance;
                    await instance.InvokeStart();
                }

                if (!_isHttps) return;
                if (instance.Certificate == null) throw new ArgumentException($"Instance {instance.DomainNames} does not have a certificate.");
                _listener.AddCertificate(domainname, instance.Certificate);
            }
        }

        public async Task Run(bool ThrowException = false)
        {
            if (_cts.Token.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }
            if (_isRunning) throw new InvalidOperationException("Listener is already running.");
            _isRunning = true;
            _listener.Start();
            if (ThrowException)
            {
                await RunThrowing();
                return;
            }
            await RunNoneThrowing();
        }

        private async Task RunThrowing()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync(_cts.Token);
                    if (ctx.Request == null) continue;
                    _ = Task.Run(async () => await ProcessConnection(ctx));
                }
                catch(OperationCanceledException)
                {
                    continue;
                }
                catch
                {
                    throw;
                }
            }
            _listener.Stop();
        }

        private async Task RunNoneThrowing()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync(_cts.Token);
                    Console.WriteLine($"[{DateTime.Now}] Connection recived from {ctx.Connection.Client.RemoteEndPoint}");
                    if (ctx.Request == null) continue;
                    _ = Task.Run(async () => await ProcessConnection(ctx));
                }
                catch(OperationCanceledException)
                {
                    continue;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception occured at {DateTime.Now} with Exception: {e} Message:{e.Message}\n\nData:\n\t{e.Data}");
                    continue;
                }
            }
            _listener.Stop();
            Console.WriteLine($"[{DateTime.Now}] Listener stopped.");
        }

        public void Pause()
        {
            if (!_isRunning) throw new InvalidOperationException("Listener is not running.");
            _cts.Cancel();
            _isRunning = false;
        }

        public async Task Shutdown()
        {
            if (!_isRunning) throw new InvalidOperationException("Listener is not running.");
            _cts.Cancel();
            _listener.Stop();
            _isRunning = false;
            await RemoveInstances(_instances.Keys);
        }

        public virtual async Task ProcessConnection(HttpsListenerContext ctx)
        {
            string host = ctx.Request!.Host;
            if (_instances.TryGetValue(host, out HttpsHandlerInstance? instance))
            {
                await instance.InvokeConnectionRecieved(instance, ctx.Connection);
                await instance.HandleConnection(ctx);
            }
            else
            {
                await ctx.Response!.SendError(404);
            }
        }
    }
}
