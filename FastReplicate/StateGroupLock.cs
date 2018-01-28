using System;
using System.Threading;
using Sandbox.Game.Replication.StateGroups;
using VRage.Network;

namespace FastReplicate
{
    public struct StateGroupLock : IDisposable
    {
        private readonly IMyStateGroup _group;

        public StateGroupLock(IMyStateGroup g)
        {
            _group = g;
            if (_group is MyCharacterPhysicsStateGroup)
                Monitor.Enter(_group);
        }

        public void Dispose()
        {
            if (_group is MyCharacterPhysicsStateGroup)
                Monitor.Exit(_group);
        }
    }
}
