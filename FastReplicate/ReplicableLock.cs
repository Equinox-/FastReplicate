using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRage.Network;

namespace FastReplicate
{
    public struct ReplicableLock : IDisposable
    {
        private readonly IMyReplicable _rep;

        public ReplicableLock(IMyReplicable r)
        {
            _rep = r;
            Monitor.Enter(r);
        }

        public void Dispose()
        {
            Monitor.Exit(_rep);
        }
    }
}
