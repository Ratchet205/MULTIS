using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multis.Https
{
    public class WebSocketHandler
    {
        //private readonly 
        public enum Opcode
        {
            Continuation = 0x0,
            Text = 0x1, //UTF-8 text
            Binary = 0x2,
            Close = 0x8,
            Ping = 0x9,
            Pong = 0xA
        }
        private readonly WebSocketConnectionList _connections = new();


        private WebSocketHandler() { }

        public Task Add(WebSocketConnection ctx)
        {
            return _connections.Add(ctx);
        }

        public async Task On(Opcode opCode , Func<HttpsListenerContext, WebSocketOutput> exec) // add some kind of 
        {
        }
    }
}
