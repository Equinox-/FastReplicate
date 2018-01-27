using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using ParallelTasks;
using Sandbox.Engine.Multiplayer;
using Torch.Collections;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage;
using VRage.Collections;
using VRage.Library.Collections;
using VRage.Library.Utils;
using VRage.Network;
using VRage.Replication;
using VRageMath;
using TClientData = System.Object;

// ReSharper disable BuiltInTypeReferenceStyle

namespace FastReplicate
{
    public class ThreadedReplicationServer : MyReplicationServer
    {
        #region Config

        public static bool UseReplicationHack = true;
        public static float TargetPacketFill = 1f;

        public static readonly MtObservableSortedDictionary<ulong, ClientStatsViewModel> ClientStats =
            new MtObservableSortedDictionary<ulong, ClientStatsViewModel>();

        #endregion

        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly MyTimeSpan _maximumPacketGap = MyTimeSpan.FromSeconds(0.40000000596046448);

        public ThreadedReplicationServer(IReplicationServerCallback callback, EndpointId? localClientEndpoint,
            bool usePlayoutDelayBuffer) : base(new ConcurrentReplicationCallback(callback), localClientEndpoint,
            usePlayoutDelayBuffer)
        {
        }

        public new TypeId GetTypeIdByType(Type t)
        {
            return base.GetTypeIdByType(t);
        }

        private void RefreshReplicable(IMyReplicable rep)
        {
            foreach (var client in ClientStates)
                client.Value.RefreshReplicable(rep, false);
        }

        public override void SendUpdate()
        {
            if (!UseReplicationHack)
            {
                base.SendUpdate();
                return;
            }

            MServerTimeStamp = Callback.GetUpdateTime();
            MServerFrame += 1L;
            if (ClientStates.Count == 0)
                return;

            Stats_ObjectsRefreshed = 0;
            Stats_ObjectsSent = 0;
            MPriorityUpdates.ApplyChanges();
            if (MPriorityUpdates.Count > 0)
            {
                TmpHash.Clear();
                foreach (IMyReplicable myReplicable in MPriorityUpdates)
                {
                    if (!myReplicable.HasToBeChild)
                        RefreshReplicable(myReplicable);
                    MPriorityUpdates.Remove(myReplicable, false);
                    TmpHash.Add(myReplicable);
                }

                foreach (KeyValuePair<Endpoint, ClientData> keyValuePair in ClientStates)
                    if (keyValuePair.Value.IsReady)
                        keyValuePair.Value.SendStateSync(TmpHash);
                TmpHash.Clear();

                MPriorityUpdates.ApplyRemovals();
                return;
            }
            
            System.Threading.Tasks.Parallel.For(0, ClientStates.Values.Count, new ParallelOptions(),
                (x) => ClientStates.Values[x].Update());


            foreach (var client in ClientStates)
            {
                if (MServerTimeStamp > client.Value.LastStateSyncTimeStamp + _maximumPacketGap)
                    client.Value.SendEmptyStateSync();
                if (MServerTimeStamp > client.Value.LastReceivedTimeStamp + _maximumPacketGap)
                    client.Value.State.ResetControlledEntityControls();
            }

            if (StressSleep.X > 0)
            {
                int millisecondsTimeout;
                if (StressSleep.Z == 0)
                {
                    millisecondsTimeout = MyRandom.Instance.Next(StressSleep.X, StressSleep.Y);
                }
                else
                {
                    millisecondsTimeout =
                        (int) (Math.Sin(MServerTimeStamp.Milliseconds * 3.1415926535897931 / StressSleep.Z) *
                               StressSleep.Y + StressSleep.X);
                }

                Thread.Sleep(millisecondsTimeout);
            }
        }

        #region Accessors

        public MyTimeSpan MServerTimeStamp
        {
            get => _serverTimeStampGetter(this);
            set => _serverTimeStampSetter(this, value);
        }

        public long MServerFrame
        {
            get => _serverFrameGetter(this);
            set => _serverFrameSetter(this, value);
        }

        public struct LockToken<T> : IDisposable
        {
            public readonly T Value;
            private readonly FastResourceLock _lock;
            private readonly bool _readOnly;

            public LockToken(T val, FastResourceLock lck, bool readOnly)
            {
                Value = val;
                _lock = lck;
                _readOnly = readOnly;
                if (_readOnly)
                    lck.AcquireShared();
                else
                    lck.AcquireExclusive();
            }

            public void Dispose()
            {
                if (_readOnly)
                    _lock.ReleaseShared();
                else
                    _lock.ReleaseExclusive();
            }
        }

        private EndpointId? MLocalClientEndpoint => _localClientEndpointGetter(this);

        private bool MUsePlayoutDelayBuffer => _usePlayoutDelayBufferGetter(this);

        public IReplicationServerCallback Callback => _callbackGetter(this);

        private Action<BitStream, EndpointId> MEventQueueSender => _eventQueueSenderGetter(this);

        private CacheList<IMyStateGroup> MTmpGroups => _tmpGroupsGetter(this);

        private HashSet<IMyReplicable> TmpHash => _tmpHashGetter(this);

        private CachingHashSet<IMyReplicable> MPostponedDestructionReplicables =>
            _postponedDestructionReplicablesGetter(this);

        private MyBandwidthLimits MLimits => _limitsGetter(this);

        public BandwidthCounter AllocateBandwidthCounter()
        {
            return new BandwidthCounter(MLimits);
        }

        private ConcurrentCachingHashSet<IMyReplicable> MPriorityUpdates => _priorityUpdatesGetter(this);

        /// <summary>
        ///     All replicables on server.
        /// </summary>
        private MyReplicablesBase MReplicables => _replicablesGetter(this);

        private readonly FastResourceLock _replicableLock = new FastResourceLock();

        public LockToken<MyReplicablesBase> BorrowReplicables(bool ro)
        {
            return new LockToken<MyReplicablesBase>(MReplicables, _replicableLock, ro);
        }

        /// <summary>
        ///     All replicable state groups.
        /// </summary>
        public Dictionary<IMyReplicable, List<IMyStateGroup>> MReplicableGroups => _replicableGroupsGetter(this);

        /// <summary>
        ///     Network objects and states which are actively replicating to clients.
        /// </summary>
        private IProxyDictionary<Endpoint, ClientData> ClientStates
        {
            get
            {
                if (_clientStatesProxy != null)
                    return _clientStatesProxy;
                var o = _clientStatesGetter(this);
                var valType = o.GetType().GetGenericArguments()[1];
                var proxyType =
                    typeof(ProxyDictionary<,,>).MakeGenericType(typeof(Endpoint), typeof(ClientData), valType);
                return _clientStatesProxy = (IProxyDictionary<Endpoint, ClientData>) Activator.CreateInstance(proxyType,
                    o, new ProxyModifier<Endpoint, TClientData, ClientData>(ClientProxyAllocator), new Action<Endpoint, ClientData>(ClientProxyDeallocator));
            }
        }

        private static void ClientProxyDeallocator(Endpoint ip, ClientData stor)
        {
            ClientStats.Remove(ip.Id.Value);
            stor.Dispose();
        }

        private bool ClientProxyAllocator(Endpoint ip, TClientData backing, ref ClientData stor)
        {
            if (stor != null)
            {
                stor.InternalClientData = backing;
                return false;
            }
            
            if (!ClientStats.TryGetValue(ip.Id.Value, out var stats))
                stats = ClientStats[ip.Id.Value] = new ClientStatsViewModel(ip.Id.Value);
            stor = new ClientData(this, ip, stats) {InternalClientData = backing};
            return true;
        }

        private IProxyDictionary<Endpoint, ClientData> _clientStatesProxy;

        /// <summary>
        ///     Clients that recently disconnected are saved here for some time so that the server doesn't freak out in case some
        ///     calls are still pending for them
        /// </summary>
        private Dictionary<Endpoint, MyTimeSpan> MRecentClientsStates => _recentClientsStatesGetter(this);

        private HashSet<Endpoint> MRecentClientStatesToRemove => _recentClientStatesToRemoveGetter(this);

#pragma warning disable 649
        [ReflectedGetter(Name = "m_serverTimeStamp")]
        private static readonly Func<MyReplicationServer, MyTimeSpan> _serverTimeStampGetter;

        [ReflectedGetter(Name = "m_serverFrame")]
        private static readonly Func<MyReplicationServer, long> _serverFrameGetter;

        [ReflectedSetter(Name = "m_serverTimeStamp")]
        private static readonly Action<MyReplicationServer, MyTimeSpan> _serverTimeStampSetter;

        [ReflectedSetter(Name = "m_serverFrame")]
        private static readonly Action<MyReplicationServer, long> _serverFrameSetter;

        [ReflectedGetter(Name = "m_localClientEndpoint")]
        private static readonly Func<MyReplicationServer, EndpointId?> _localClientEndpointGetter;

        [ReflectedGetter(Name = "m_usePlayoutDelayBuffer")]
        private static readonly Func<MyReplicationServer, bool> _usePlayoutDelayBufferGetter;

        [ReflectedGetter(Name = "m_callback")]
        private static readonly Func<MyReplicationServer, IReplicationServerCallback> _callbackGetter;

        [ReflectedGetter(Name = "m_eventQueueSender")]
        private static readonly Func<MyReplicationServer, Action<BitStream, EndpointId>> _eventQueueSenderGetter;

        [ReflectedGetter(Name = "m_tmpGroups")]
        private static readonly Func<MyReplicationServer, CacheList<IMyStateGroup>> _tmpGroupsGetter;

        [ReflectedGetter(Name = "m_tmpSortEntries")]
        private static readonly Func<MyReplicationServer, CacheList<MyStateDataEntry>> _tmpSortEntriesGetter;

        [ReflectedGetter(Name = "m_tmpStreamingEntries")]
        private static readonly Func<MyReplicationServer, CacheList<MyStateDataEntry>> _tmpStreamingEntriesGetter;

        [ReflectedGetter(Name = "m_tmpSentEntries")]
        private static readonly Func<MyReplicationServer, CacheList<MyStateDataEntry>> _tmpSentEntriesGetter;

        [ReflectedGetter(Name = "m_toDeleteHash")]
        private static readonly Func<MyReplicationServer, HashSet<IMyReplicable>> _toDeleteHashGetter;

        [ReflectedGetter(Name = "m_tmp")]
        private static readonly Func<MyReplicationServer, CacheList<IMyReplicable>> _tmpGetter;

        [ReflectedGetter(Name = "m_tmpHash")]
        private static readonly Func<MyReplicationServer, HashSet<IMyReplicable>> _tmpHashGetter;

        [ReflectedGetter(Name = "m_layerUpdateHash")]
        private static readonly Func<MyReplicationServer, HashSet<IMyReplicable>> _layerUpdateHashGetter;

        [ReflectedGetter(Name = "m_lastLayerAdditions")]
        private static readonly Func<MyReplicationServer, HashSet<IMyReplicable>> _lastLayerAdditionsGetter;

        [ReflectedGetter(Name = "m_postponedDestructionReplicables")]
        private static readonly Func<MyReplicationServer, CachingHashSet<IMyReplicable>>
            _postponedDestructionReplicablesGetter;

        [ReflectedGetter(Name = "m_limits")]
        private static readonly Func<MyReplicationServer, MyBandwidthLimits> _limitsGetter;

        [ReflectedGetter(Name = "m_priorityUpdates")]
        private static readonly Func<MyReplicationServer, ConcurrentCachingHashSet<IMyReplicable>>
            _priorityUpdatesGetter;

        [ReflectedGetter(Name = "m_replicables")]
        private static readonly Func<MyReplicationServer, MyReplicablesBase> _replicablesGetter;

        [ReflectedGetter(Name = "m_replicableGroups")]
        private static readonly Func<MyReplicationServer, Dictionary<IMyReplicable, List<IMyStateGroup>>>
            _replicableGroupsGetter;

        [ReflectedGetter(Name = "m_clientStates")]
        private static readonly Func<MyReplicationServer, IDictionary> _clientStatesGetter;

        [ReflectedGetter(Name = "m_recentClientsStates")]
        private static readonly Func<MyReplicationServer, Dictionary<Endpoint, MyTimeSpan>> _recentClientsStatesGetter;

        [ReflectedGetter(Name = "m_recentClientStatesToRemove")]
        private static readonly Func<MyReplicationServer, HashSet<Endpoint>> _recentClientStatesToRemoveGetter;

        [ReflectedGetter(Name = "m_replicationPaused")]
        private static readonly Func<MyReplicationServer, bool> _replicationPausedGetter;
#pragma warning restore 649

        public bool MReplicationPaused => _replicationPausedGetter(this);

        private interface IProxyDictionary<TK, TV> : IEnumerable<KeyValuePair<TK, TV>>
        {
            IReadOnlyList<TV> Values { get; }
            int Count { get; }
        }

        public delegate bool ProxyModifier<in TK, in TB, TV>(TK key, TB value, ref TV proxy);

        private class ProxyDictionary<TK, TV, TB> : IProxyDictionary<TK, TV>
        {
            private readonly ProxyModifier<TK, TB, TV> _proxyModifier;
            private readonly Dictionary<TK, TV> _proxyStorage;
            private readonly Dictionary<TK, TB> _backing;
            private readonly List<TV> _values;

            public IReadOnlyList<TV> Values
            {
                get
                {
                    CheckProxyTable();
                    return _values;
                }
            }

            private static readonly Func<Dictionary<TK, TB>, int> _backingVersionGet =
                FieldAccess.CreateGetter<Dictionary<TK, TB>, int>(
                    typeof(Dictionary<TK, TB>).GetField("version", BindingFlags.Instance | BindingFlags.NonPublic));

            private readonly ThreadLocal<HashSet<TK>> _tmpRemoval;
            private readonly Action<TK, TV> _deallocator;

            private int _lastVersion;

            public ProxyDictionary(Dictionary<TK, TB> backing, ProxyModifier<TK, TB, TV> proxyAllocator, Action<TK, TV> deallocator)
            {
                _values = new List<TV>();
                _backing = backing;
                _proxyStorage = new Dictionary<TK, TV>(_backing.Comparer);
                _proxyModifier = proxyAllocator;
                _tmpRemoval = new ThreadLocal<HashSet<TK>>(() => new HashSet<TK>(_backing.Comparer));
                _deallocator = deallocator;
                CheckProxyTable(true);
            }

            private void CheckProxyTable(bool force = false)
            {
                var ver = _backingVersionGet(_backing);
                if (!force && _lastVersion == ver)
                    return;

                _lastVersion = ver;
                var toRemove = _tmpRemoval.Value;
                toRemove.Clear();
                foreach (var l in _proxyStorage)
                    if (!_backing.ContainsKey(l.Key))
                    {
                        _values.Remove(l.Value);
                        _deallocator(l.Key, l.Value);
                        toRemove.Add(l.Key);
                    }

                foreach (var l in toRemove)
                    _proxyStorage.Remove(l);

                toRemove.Clear();

                foreach (var l in _backing)
                {
                    var exists = _proxyStorage.TryGetValue(l.Key, out var tmp);
                    if (_proxyModifier.Invoke(l.Key, l.Value, ref tmp))
                    {
                        if (tmp != null && !exists)
                            _values.Add(tmp);
                        _proxyStorage[l.Key] = tmp;
                    }
                }
            }

            public int Count
            {
                get
                {
                    CheckProxyTable();
                    return _backing.Count;
                }
            }

            public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
            {
                CheckProxyTable();
                return _proxyStorage.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        #endregion
    }
}