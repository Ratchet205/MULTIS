using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Https
{
    public class WebSocketConnection
    {
        private readonly Stream _connectionStream;
        public Stream ConnectionStream => _connectionStream;

        private TcpClient _tcpClient = null!;
        public TcpClient Client => _tcpClient;

        public WebSocketConnection(HttpsListenerContext ctx)
        {
            _connectionStream = ctx.Response!.OutputStream;
        }
    }

    internal sealed class WebSocketConnectionList
    {
        private readonly Dictionary<EndPoint, WebSocketConnection> _connections = [];
        private readonly object _lock = new();

        public bool Contains(WebSocketContext c)
        {
            return _connections.ContainsKey(c.Connection.Client.Client.RemoteEndPoint!);
        }

        public async Task Add(WebSocketContext connection)
        {
            //await Task.Run(() =>
            //{
            //    connection.Close += Finish;
            //    lock (_lock)
            //    {
            //        _connections.Add(connection.Connection.Client.RemoteEndPoint!, connection);
            //    }
            //});
            //_ = connection.AliveTillClose();
            //Console.WriteLine($"\n\n\n{connection.Connection.Client.RemoteEndPoint} connection created and saved. AliveTillClose running.\n\n\n");
            //Console.WriteLine($"Connections count: {_connections.Count}\n\n");
            //foreach (var con in _connections)
            //{
            //    Console.WriteLine($"Connection: {con.Key}");
            //}
        }

        private void Finish(HttpsListenerContext hkac)
        {
            hkac.Close -= Finish;
            lock (_lock)
            {
                _connections.Remove(hkac.Connection.Client.RemoteEndPoint!);
            }
            Console.WriteLine($"\n\n\n{hkac.Connection.Client.RemoteEndPoint} connection closed and removed.\n\n\n");
        }

        public async Task Remove(HttpsListenerContext connection)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    _connections.Remove(connection.Connection.Client.RemoteEndPoint!);
                }
            });
        }
    }
}
