using System.Net;
using System.Runtime.CompilerServices;

namespace Multis.Core
{
    public interface IRunnableServer
    {
        public Task Run();
        public void Stop();
        public bool IsRunning { get; }
    }
}
