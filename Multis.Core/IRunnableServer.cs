using System.Net;

namespace Multis.Core
{
    public interface IRunnableServer
    {
        public Task Run();
        public void Stop();
    }
}
