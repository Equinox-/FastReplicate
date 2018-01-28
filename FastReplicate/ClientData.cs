using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using Torch.Utils;
using VRage.Collections;
using VRage.Library.Collections;
using VRage.Library.Utils;
using VRage.Network;
using VRage.Replication;
using VRageMath;

namespace FastReplicate
{
    public class ClientData : IDisposable
    {
        #region Accessors

        public const string InternalTypeName = "VRage.Network.MyReplicationServer+ClientData, VRage";
#pragma warning disable 649
        [ReflectedGetter(Name = "State", TypeName = InternalTypeName)]
        private static readonly Func<object, MyClientStateBase> _stateGetter;

        [ReflectedGetter(Name = "EventQueue", TypeName = InternalTypeName)]
        private static readonly Func<object, MyPacketQueue> _eventQueueGetter;

        [ReflectedGetter(Name = "PermanentReplicables", TypeName = InternalTypeName)]
        private static readonly Func<object, Dictionary<IMyReplicable, byte>> _permanentReplicablesGetter;

        [ReflectedGetter(Name = "Replicables", TypeName = InternalTypeName)]
        private static readonly Func<object, MyConcurrentDictionary<IMyReplicable, MyReplicableClientData>>
            _replicablesGetter;

        [ReflectedGetter(Name = "BlockedReplicables", TypeName = InternalTypeName)]
        private static readonly Func<object, object> _blockedReplicablesGetter;

        [ReflectedGetter(Name = "StateGroups", TypeName = InternalTypeName)]
        private static readonly Func<object, MyConcurrentDictionary<IMyStateGroup, MyStateDataEntry>>
            _stateGroupsGetter;

        [ReflectedGetter(Name = "DirtyGroups", TypeName = InternalTypeName)]
        private static readonly Func<object, MyConcurrentHashSet<IMyStateGroup>> _dirtyGroupsGetter;

        [ReflectedGetter(Name = "DirtyGroupsToRemove", TypeName = InternalTypeName)]
        private static readonly Func<object, List<IMyStateGroup>> _dirtyGroupsToRemoveGetter;

        [ReflectedGetter(Name = "PausedReplicables", TypeName = InternalTypeName)]
        private static readonly Func<object, HashSet<IMyReplicable>> _pausedReplicablesGetter;

        [ReflectedGetter(Name = "ClientCachedData", TypeName = InternalTypeName)]
        private static readonly Func<object, HashSet<string>> _clientCachedDataGetter;

        [ReflectedGetter(Name = "StateSyncPacketId", TypeName = InternalTypeName)]
        private static readonly Func<object, byte> _stateSyncPacketIdGetter;

        [ReflectedSetter(Name = "StateSyncPacketId", TypeName = InternalTypeName)]
        private static readonly Action<object, byte> _stateSyncPacketIdSetter;

        [ReflectedGetter(Name = "LastReceivedAckId", TypeName = InternalTypeName)]
        private static readonly Func<object, byte> _lastReceivedAckIdGetter;

        [ReflectedSetter(Name = "LastReceivedAckId", TypeName = InternalTypeName)]
        private static readonly Action<object, byte> _lastReceivedAckIdSetter;

        [ReflectedGetter(Name = "LastStateSyncPacketId", TypeName = InternalTypeName)]
        private static readonly Func<object, byte> _lastStateSyncPacketIdGetter;

        [ReflectedSetter(Name = "LastStateSyncPacketId", TypeName = InternalTypeName)]
        private static readonly Action<object, byte> _lastStateSyncPacketIdSetter;

        [ReflectedGetter(Name = "LastClientPacketId", TypeName = InternalTypeName)]
        private static readonly Func<object, byte> _lastClientPacketIdGetter;

        [ReflectedSetter(Name = "LastClientPacketId", TypeName = InternalTypeName)]
        private static readonly Action<object, byte> _lastClientPacketIdSetter;

        [ReflectedGetter(Name = "LastClientRealtime", TypeName = InternalTypeName)]
        private static readonly Func<object, MyTimeSpan> _lastClientRealtimeGetter;

        [ReflectedSetter(Name = "LastClientRealtime", TypeName = InternalTypeName)]
        private static readonly Action<object, MyTimeSpan> _lastClientRealtimeSetter;

        [ReflectedGetter(Name = "WaitingForReset", TypeName = InternalTypeName)]
        private static readonly Func<object, bool> _waitingForResetGetter;

        [ReflectedSetter(Name = "WaitingForReset", TypeName = InternalTypeName)]
        private static readonly Action<object, bool> _waitingForResetSetter;

        [ReflectedGetter(Name = "IsReady", TypeName = InternalTypeName)]
        private static readonly Func<object, bool> _isReadyGetter;

        [ReflectedSetter(Name = "IsReady", TypeName = InternalTypeName)]
        private static readonly Action<object, bool> _isReadySetter;

        [ReflectedGetter(Name = "LastProcessedClientPacketId", TypeName = InternalTypeName)]
        private static readonly Func<object, byte> _lastProcessedClientPacketIdGetter;

        [ReflectedSetter(Name = "LastProcessedClientPacketId", TypeName = InternalTypeName)]
        private static readonly Action<object, byte> _lastProcessedClientPacketIdSetter;

        [ReflectedGetter(Name = "StartingServerTimeStamp", TypeName = InternalTypeName)]
        private static readonly Func<object, MyTimeSpan> _startingServerTimeStampGetter;

        [ReflectedSetter(Name = "StartingServerTimeStamp", TypeName = InternalTypeName)]
        private static readonly Action<object, MyTimeSpan> _startingServerTimeStampSetter;

        [ReflectedGetter(Name = "LastReceivedTimeStamp", TypeName = InternalTypeName)]
        private static readonly Func<object, MyTimeSpan> _lastReceivedTimeStampGetter;

        [ReflectedSetter(Name = "LastReceivedTimeStamp", TypeName = InternalTypeName)]
        private static readonly Action<object, MyTimeSpan> _lastReceivedTimeStampSetter;

        [ReflectedGetter(Name = "PriorityMultiplier", TypeName = InternalTypeName)]
        private static readonly Func<object, float> _priorityMultiplierGetter;

        [ReflectedSetter(Name = "PriorityMultiplier", TypeName = InternalTypeName)]
        private static readonly Action<object, float> _priorityMultiplierSetter;

        [ReflectedGetter(Name = "UpdateLayers", TypeName = InternalTypeName)]
        private static readonly Func<object, MyReplicationServer.UpdateLayer[]> _updateLayersGetter;

        [ReflectedSetter(Name = "UpdateLayers", TypeName = InternalTypeName)]
        private static readonly Action<object, MyReplicationServer.UpdateLayer[]> _updateLayersSetter;

        [ReflectedGetter(Name = "PendingStateSyncAcks", TypeName = InternalTypeName)]
        private static readonly Func<object, List<IMyStateGroup>[]> _pendingStateSyncAcksGetter;

        [ReflectedGetter(Name = "ProcessedPacket", TypeName = InternalTypeName)]
        private static readonly Func<object, bool> _processedPacketGetter;

        [ReflectedSetter(Name = "ProcessedPacket", TypeName = InternalTypeName)]
        private static readonly Action<object, bool> _processedPacketSetter;

        [ReflectedGetter(Name = "LastStateSyncTimeStamp", TypeName = InternalTypeName)]
        private static readonly Func<object, MyTimeSpan> _lastStateSyncTimeStampGetter;

        [ReflectedSetter(Name = "LastStateSyncTimeStamp", TypeName = InternalTypeName)]
        private static readonly Action<object, MyTimeSpan> _lastStateSyncTimeStampSetter;

        [ReflectedGetter(Name = "ClientTracker", TypeName = InternalTypeName)]
        private static readonly Func<object, MyPacketTracker> _clientTrackerGetter;

        [ReflectedSetter(Name = "ClientTracker", TypeName = InternalTypeName)]
        private static readonly Action<object, MyPacketTracker> _clientTrackerSetter;

        [ReflectedGetter(Name = "ClientStats", TypeName = InternalTypeName)]
        private static readonly Func<object, MyPacketStatistics> _clientStatsGetter;

        [ReflectedSetter(Name = "ClientStats", TypeName = InternalTypeName)]
        private static readonly Action<object, MyPacketStatistics> _clientStatsSetter;

        [ReflectedGetter(Name = "Islands", TypeName = InternalTypeName)]
        private static readonly Func<object, object> _islandsGetter;

        [ReflectedGetter(Name = "ReplicableToIsland", TypeName = InternalTypeName)]
        private static readonly Func<object, IDictionary> _replicableToIslandGetter;

        [ReflectedMethod(Name = "Remove", OverrideTypes = new[] {typeof(IMyReplicable)},
            TypeName =
                "VRage.Collections.MyConcurrentDictionary`2[[VRage.Network.IMyReplicable, VRage], [VRage.Network.MyReplicationServer+MyDestroyBlocker, VRage]], VRage.Library")]
        private static readonly Func<object, IMyReplicable, bool> _blockedReplicablesRemove;
#pragma warning disable 649

        public object InternalClientData;


        public MyClientStateBase State => _stateGetter(InternalClientData);


        public MyPacketQueue EventQueue => _eventQueueGetter(InternalClientData);


        public Dictionary<IMyReplicable, byte> PermanentReplicables =>
            _permanentReplicablesGetter(InternalClientData);


        public MyConcurrentDictionary<IMyReplicable, MyReplicableClientData> Replicables =>
            _replicablesGetter(InternalClientData);


        public object BlockedReplicables => _blockedReplicablesGetter(InternalClientData);


        public MyConcurrentDictionary<IMyStateGroup, MyStateDataEntry> StateGroups =>
            _stateGroupsGetter(InternalClientData);

        public object Islands => _islandsGetter(InternalClientData);

        public IDictionary ReplicableToIsland => _replicableToIslandGetter(InternalClientData);


        public MyConcurrentHashSet<IMyStateGroup> DirtyGroups => _dirtyGroupsGetter(InternalClientData);


        public List<IMyStateGroup> DirtyGroupsToRemove => _dirtyGroupsToRemoveGetter(InternalClientData);


        public HashSet<IMyReplicable> PausedReplicables => _pausedReplicablesGetter(InternalClientData);


        public HashSet<string> ClientCachedData => _clientCachedDataGetter(InternalClientData);


        public byte StateSyncPacketId
        {
            get => _stateSyncPacketIdGetter(InternalClientData);
            set => _stateSyncPacketIdSetter(InternalClientData, value);
        }


        private byte LastReceivedAckId
        {
            get => _lastReceivedAckIdGetter(InternalClientData);
            set => _lastReceivedAckIdSetter(InternalClientData, value);
        }


        private byte LastStateSyncPacketId
        {
            get => _lastStateSyncPacketIdGetter(InternalClientData);
            set => _lastStateSyncPacketIdSetter(InternalClientData, value);
        }


        public byte LastClientPacketId
        {
            get => _lastClientPacketIdGetter(InternalClientData);
            set => _lastClientPacketIdSetter(InternalClientData, value);
        }


        public MyTimeSpan LastClientRealtime
        {
            get => _lastClientRealtimeGetter(InternalClientData);
            set => _lastClientRealtimeSetter(InternalClientData, value);
        }


        public bool WaitingForReset
        {
            get => _waitingForResetGetter(InternalClientData);
            set => _waitingForResetSetter(InternalClientData, value);
        }


        public bool IsReady
        {
            get => _isReadyGetter(InternalClientData);
            set => _isReadySetter(InternalClientData, value);
        }


        public byte LastProcessedClientPacketId
        {
            get => _lastProcessedClientPacketIdGetter(InternalClientData);
            set => _lastProcessedClientPacketIdSetter(InternalClientData, value);
        }


        public MyTimeSpan StartingServerTimeStamp
        {
            get => _startingServerTimeStampGetter(InternalClientData);
            set => _startingServerTimeStampSetter(InternalClientData, value);
        }


        public MyTimeSpan LastReceivedTimeStamp
        {
            get => _lastReceivedTimeStampGetter(InternalClientData);
            set => _lastReceivedTimeStampSetter(InternalClientData, value);
        }


        public float PriorityMultiplier
        {
            get => _priorityMultiplierGetter(InternalClientData);
            set => _priorityMultiplierSetter(InternalClientData, value);
        }


        public MyReplicationServer.UpdateLayer[] UpdateLayers
        {
            get => _updateLayersGetter(InternalClientData);
            set => _updateLayersSetter(InternalClientData, value);
        }

        public MyPacketStatistics ClientStats
        {
            get => _clientStatsGetter(InternalClientData);
            set => _clientStatsSetter(InternalClientData, value);
        }


        public List<IMyStateGroup>[] PendingStateSyncAcks => _pendingStateSyncAcksGetter(InternalClientData);


        public bool ProcessedPacket
        {
            get => _processedPacketGetter(InternalClientData);
            set => _processedPacketSetter(InternalClientData, value);
        }


        public MyTimeSpan LastStateSyncTimeStamp
        {
            get => _lastStateSyncTimeStampGetter(InternalClientData);
            set => _lastStateSyncTimeStampSetter(InternalClientData, value);
        }


        public MyPacketTracker ClientTracker
        {
            get => _clientTrackerGetter(InternalClientData);
            set => _clientTrackerSetter(InternalClientData, value);
        }

        public bool HasReplicable(IMyReplicable replicable)
        {
            return Replicables.ContainsKey(replicable);
        }

        #endregion

        private readonly HashSet<IMyReplicable> _layerUpdateHash = new HashSet<IMyReplicable>();
        private readonly HashSet<IMyReplicable> _toDeleteHash = new HashSet<IMyReplicable>();
        private readonly HashSet<IMyReplicable> _lastLayerAdditions = new HashSet<IMyReplicable>();
        private readonly HashSet<IMyReplicable> _replicablesToSend = new HashSet<IMyReplicable>();

        private readonly ThreadedReplicationServer _server;
        private readonly Endpoint _clientEndpoint;
        private readonly BitStream _sendStream = new BitStream();
        private readonly BandwidthCounter _bandwidthCounter;
        private readonly ClientStatsViewModel _stats;
        private long _totalBitsSent;

        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public ClientData(ThreadedReplicationServer server, Endpoint endpoint, ClientStatsViewModel stats)
        {
            _server = server;
            _clientEndpoint = endpoint;
            _bandwidthCounter = server.AllocateBandwidthCounter();
            _stats = stats;
        }

        #region Ticking Updates

        public void Update()
        {
            if (!IsReady)
                return;
            try
            {
                UpdateNearbyReplicables();
                SendReplicables();
                _stats.TotalBitsSent = _totalBitsSent;
                _stats.TickAverages();
                _totalBitsSent = 0;
            }
            catch (Exception e)
            {
                _log.Error(e.ToString());
            }
        }

        private void UpdateNearbyReplicables()
        {
            _layerUpdateHash.Clear();
            _toDeleteHash.Clear();
            _lastLayerAdditions.Clear();
            var controlledReplicable = State.ControlledReplicable;
            var characterReplicable = State.CharacterReplicable;
            for (var layerIndex = 0; layerIndex < UpdateLayers.Length; layerIndex++)
            {
                MyReplicationServer.UpdateLayer layer = UpdateLayers[layerIndex];
                BoundingBoxD aabb = new BoundingBoxD(State.Position - layer.Descriptor.Radius,
                    State.Position + layer.Descriptor.Radius);
                using (var repBase = _server.BorrowReplicables(true))
                    repBase.Value.GetReplicablesInBox(aabb, layer.Updater.List);
                foreach (IMyReplicable item in layer.Replicables)
                    if (!_layerUpdateHash.Contains(item))
                        _toDeleteHash.Add(item);

                layer.Replicables.Clear();

                foreach (var rep in layer.Updater.List)
                    AddReplicableToLayer(rep, layer);

                foreach (KeyValuePair<IMyReplicable, byte> kv in PermanentReplicables)
                    if (kv.Value == layerIndex)
                        AddReplicableToLayer(kv.Key, layer);

                // ensure the highest res layer has controlled info
                if (layerIndex == 0)
                {
                    if (controlledReplicable != null)
                        AddReplicableToLayer(controlledReplicable, layer);
                    if (characterReplicable != null)
                        AddReplicableToLayer(characterReplicable, layer);
                }

                // the lowest res has all replicables
                if (layerIndex == UpdateLayers.Length - 1)
                {
                    foreach (var rep in _lastLayerAdditions)
                        AddReplicableToLayer(rep, layer);
                }

                layer.Updater.List.Set(layer.Replicables);
                layer.Sender.List.Set(layer.Replicables);

                layer.Updater.Update();
                layer.Updater.Iterate((rep) => { RefreshReplicable(rep, true); });
            }

            foreach (IMyReplicable toDelete in _toDeleteHash)
                if (HasReplicable(toDelete))
                    RemoveForClient(toDelete, true);

            _stats.TotalRoots = _lastLayerAdditions.Count;
            _toDeleteHash.Clear();
        }

        private void SendReplicables()
        {
            _replicablesToSend.Clear();
            foreach (var layer in UpdateLayers)
            {
                layer.Sender.Update();
                layer.Sender.Iterate((x) => _replicablesToSend.Add(x));
            }

            if (_replicablesToSend.Count == 0)
                return;
            SendStateSync(_replicablesToSend);
            _replicablesToSend.Clear();
        }

        #endregion

        #region Network API

        public void SendEmptyStateSync()
        {
            WritePacketHeader(false);
            _totalBitsSent += _sendStream.BitPosition;
            _server.Callback.SendStateSync(_sendStream, State.EndpointId, false);
        }

        private void CleanupIslands()
        {
            foreach (IMyStateGroup value in DirtyGroupsToRemove)
                DirtyGroups.Remove(value);

            DirtyGroupsToRemove.Clear();
            using (var islandEnumerator = GeneratedMethods.ClientDataIslandsEnumerator(Islands))
            {
                while (islandEnumerator.MoveNext())
                {
                    var island = islandEnumerator.Current;
                    bool flag = true;
                    foreach (IMyStreamableReplicable rep in island.Replicables)
                    {
                        if (DirtyGroups.Contains(rep.GetStreamingStateGroup()))
                        {
                            flag = false;
                            break;
                        }
                    }

                    if (flag)
                    {
                        SendReplicationIslandDone(island.Index);
                        GeneratedMethods.RemoveCachedIsland(InternalClientData, island);
                    }
                }
            }

            GeneratedMethods.ClientDataIslandsApplyRemovals(Islands);
        }

        public void SendStateSync(HashSet<IMyReplicable> replicables)
        {
            if (StateGroups.Count == 0 || DirtyGroups.Count == 0)
            {
                _stats.StreamingRoots = 0;
                _stats.StaticReplicables = 0;
                _stats.StaticPacketsSent = 0;
                _stats.StreamingPacketsSent = 0;
                _stats.StaticReplicablesUnsent = replicables.Count;
                return;
            }

            EventQueue.Send();
            using (var streaming = CollectionCache<List<MyStateDataEntry>, MyStateDataEntry>.Borrow())
            {
                using (var sorted = CollectionCache<List<MyStateDataEntry>, MyStateDataEntry>.Borrow())
                {
                    FillEntries(replicables, streaming.Value, sorted.Value);

                    _stats.StreamingRoots = streaming.Value.Count;
                    _stats.StaticReplicables = sorted.Value.Count;

                    byte b = (byte) (LastReceivedAckId - 6);
                    byte b2 = (byte) (StateSyncPacketId + 1);
                    if (WaitingForReset || b2 == b)
                    {
                        _stats.StaticPacketsSent = 0;
                        _stats.StreamingPacketsSent = 0;
                        _stats.StaticReplicablesUnsent = sorted.Value.Count;
                        return;
                    }

                    var staticPacketsSent = 0;
                    while (SendStateSync(sorted.Value) && staticPacketsSent <= 7)
                        staticPacketsSent++;
                    staticPacketsSent++; // because of order.

                    var streamingPacketsSent = 0;


                    if (streaming.Value.Count > 0)
                        SendStreamingEntries(streaming.Value, out streamingPacketsSent);
                    else
                        streamingPacketsSent = 0;

                    _stats.StaticPacketsSent = staticPacketsSent;
                    _stats.StreamingPacketsSent = streamingPacketsSent;
                    _stats.StaticReplicablesUnsent = sorted.Value.Count;
                }
            }

            CleanupIslands();
        }

        #endregion

        #region State API

        public void RefreshReplicable(IMyReplicable replicable, bool checkDependencies = false)
        {
            if (!IsReady)
                return;

            var updateTime = _server.Callback.GetUpdateTime();

            bool exists = Replicables.TryGetValue(replicable, out MyReplicableClientData data);
            float priority = replicable.GetPriority(GeneratedMethods.AllocateClientInfo(InternalClientData), false);
            if (exists)
                data.Priority = priority;

            bool shouldExist = priority > 0f;
            if (shouldExist && !exists)
            {
                AddForClient(replicable, priority, false, checkDependencies);
            }
            else if (exists)
            {
                data.UpdateSleep(shouldExist, updateTime);
                if (data.ShouldRemove(updateTime, _server.MaxSleepTime))
                    RemoveForClient(replicable, true);
            }

            _server.Stats_ObjectsRefreshed++;
        }

        public void AddForClient(IMyReplicable replicable, float priority, bool force, bool addDependencies = false)
        {
            if (!replicable.IsReadyForReplication || HasReplicable(replicable))
                return;

            AddClientReplicable(replicable, priority, force);
            SendReplicationCreate(replicable);
            var streamable = replicable as IMyStreamableReplicable;
            if (streamable == null)
            {
                using (var list = CollectionCache<List<IMyReplicable>, IMyReplicable>.Borrow())
                {
                    using (var repBase = _server.BorrowReplicables(true))
                    {
                        list.SetEnumerated(repBase.Value.GetChildren(replicable));
                    }

                    foreach (IMyReplicable child in list.Value)
                        AddForClient(child, priority, force);
                }
            }

            using (var list = CollectionCache<List<IMyReplicable>, IMyReplicable>.Borrow())
            {
                using (new ReplicableLock(replicable))
                {
                    using (var repBase = _server.BorrowReplicables(true))
                    {
                        var physDeps = replicable.GetPhysicalDependencies(_server.MServerTimeStamp, repBase.Value);
                        if (physDeps == null || physDeps.Count == 0)
                            return;

                        if (streamable == null || !ReplicableToIsland.Contains(streamable))
                            GeneratedMethods.CreateNewCachedIsland(InternalClientData, replicable, physDeps, _server.MServerTimeStamp);

                        list.Set(physDeps);
                    }
                }

                if (addDependencies)
                    foreach (IMyReplicable physDep in list.Value)
                        AddForClient(physDep, priority, force);
            }
        }

        public void RemoveForClient(IMyReplicable replicable, bool sendDestroyToClient)
        {
            using (var repBase = _server.BorrowReplicables(false))
                repBase.Value.RefreshChildrenHierarchy(replicable);
            using (var list = CollectionCache<List<IMyReplicable>, IMyReplicable>.Borrow())
            {
                using (var repBase = _server.BorrowReplicables(true))
                    repBase.Value.GetAllChildren(replicable, list.Value);
                list.Value.Add(replicable);
                foreach (var child in list.Value)
                {
                    _blockedReplicablesRemove(BlockedReplicables, child);
                    if (sendDestroyToClient)
                        SendReplicationDestroy(child);

                    RemoveClientReplicable(child);
                }

                foreach (MyReplicationServer.UpdateLayer updateLayer in UpdateLayers)
                    updateLayer.Replicables.Remove(replicable);
            }
        }

        #endregion

        #region Layer API

        private void AddReplicableToLayer(IMyReplicable rep, MyReplicationServer.UpdateLayer layer)
        {
            if (!AddReplicableToLayerSingle(rep, layer))
                return;
            using (var list = CollectionCache<List<IMyReplicable>, IMyReplicable>.Borrow())
            {
                using (new ReplicableLock(rep))
                {
                    using (var repBase = _server.BorrowReplicables(true))
                    {
                        var physDeps = rep.GetPhysicalDependencies(_server.MServerTimeStamp, repBase.Value);
                        if (physDeps == null || physDeps.Count == 0)
                            return;
                        list.Set(physDeps);
                    }
                }

                foreach (IMyReplicable dependency in list.Value)
                    AddReplicableToLayerSingle(dependency, layer);
            }
        }

        private bool AddReplicableToLayerSingle(IMyReplicable rep, MyReplicationServer.UpdateLayer layer)
        {
            if (!_layerUpdateHash.Add(rep))
                return false;

            layer.Replicables.Add(rep);
            _toDeleteHash.Remove(rep);

            using (new ReplicableLock(rep))
            {
                HashSet<IMyReplicable> dependencies = rep.GetDependencies();
                if (dependencies == null)
                    return true;
                foreach (IMyReplicable item in dependencies)
                    _lastLayerAdditions.Add(item);
            }

            return true;
        }

        private struct ReplicableLock : IDisposable
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

        #endregion

        private void FillEntries(ICollection<IMyReplicable> replicables, List<MyStateDataEntry> streaming,
            List<MyStateDataEntry> sorted)
        {
            var clientInfo = GeneratedMethods.AllocateClientInfo(InternalClientData);
            foreach (IMyStateGroup myStateGroup in DirtyGroups)
            {
                if (replicables.Contains(myStateGroup.Owner.GetParent() ?? myStateGroup.Owner) &&
                    Replicables.TryGetValue(myStateGroup.Owner, out var clientData) &&
                    (clientData.HasActiveStateSync || myStateGroup.GroupType == StateGroupEnum.Streaming))
                {
                    MyStateDataEntry myStateDataEntry = StateGroups[myStateGroup];
                    myStateDataEntry.Priority = myStateDataEntry.Group.GetGroupPriority(
                        (int) (_server.MServerFrame - myStateDataEntry.LastSyncedFrame), clientInfo);
                    if (myStateDataEntry.Priority > 0f &&
                        !myStateDataEntry.Group.IsProcessingForClient(State.EndpointId))
                    {
                        if (myStateDataEntry.Group.GroupType == StateGroupEnum.Streaming)
                        {
                            streaming.Add(myStateDataEntry);
                            _server.Stats_ObjectsSent++;
                        }
                        else
                        {
                            sorted.Add(myStateDataEntry);
                            _server.Stats_ObjectsSent++;
                        }
                    }

                    if (!myStateGroup.IsStillDirty(State.EndpointId))
                    {
                        DirtyGroupsToRemove.Add(myStateGroup);
                    }
                }
            }

            sorted.Sort(MyStateDataEntryComparer.Instance);
            streaming.Sort(MyStateDataEntryComparer.Instance);
        }

        #region Network Internals

        private MyTimeSpan WritePacketHeader(bool streaming)
        {
            LastStateSyncTimeStamp = _server.MServerTimeStamp;
            if (!streaming)
            {
                StateSyncPacketId += 1;
            }

            if (StartingServerTimeStamp == MyTimeSpan.Zero)
            {
                StartingServerTimeStamp = _server.MServerTimeStamp;
            }

            MyTimeSpan result = _server.MServerTimeStamp - StartingServerTimeStamp;
            _sendStream.ResetWrite();
            _sendStream.WriteBool(streaming);
            _sendStream.WriteByte(streaming ? (byte) 0 : StateSyncPacketId);
            ClientStats.Write(_sendStream);
            ClientStats.Reset();
            _sendStream.WriteDouble(result.Milliseconds);
            _sendStream.WriteDouble(LastClientRealtime.Milliseconds);
            LastClientRealtime = MyTimeSpan.FromMilliseconds(-1.0);
            _server.Callback.SendCustomState(_sendStream);
            return result;
        }

        private bool SendStateSync(IList<MyStateDataEntry> toSend, bool removeSent = true)
        {
            MyTimeSpan timestamp = WritePacketHeader(false);
            int mtusize = _server.Callback.GetMTUSize(State.EndpointId);
            int maxBitsToSend = 8 * (mtusize - 8 - 1);
            int maxEntriesToSend = mtusize / 8;
            int entriesSent = 0;
            _bandwidthCounter.Clear();
            int acksSent = 0;

            var maxBitsToSerialize =
                (int) (maxBitsToSend * MathHelper.Clamp(ThreadedReplicationServer.TargetPacketFill, 0.25f, 1));

            using (var removedIndices = CollectionCache<List<int>, int>.Borrow())
            {
                for (var index = 0; index < toSend.Count; index++)
                {
                    MyStateDataEntry send = toSend[index];

                    int originalPosition = _sendStream.BitPosition;
                    _sendStream.WriteNetworkId(send.GroupId);
                    using (new StateGroupLock(send.Group))
                        send.Group.Serialize(_sendStream, State.EndpointId, timestamp,
                            StateSyncPacketId, maxBitsToSend, ClientCachedData);
                    int totalBits = _sendStream.BitPosition - originalPosition;
                    if (totalBits > 0 && _sendStream.BitPosition <= maxBitsToSend &&
                        _bandwidthCounter.Add(send.Group.GroupType, totalBits))
                    {
                        PendingStateSyncAcks[StateSyncPacketId].Add(send.Group);
                        entriesSent++;
                        send.LastSyncedFrame = _server.MServerFrame;
                        removedIndices.Value.Add(index);
                    }
                    else
                    {
                        acksSent++;
                        send.Group.OnAck(State, StateSyncPacketId, false);
                        _sendStream.SetBitPositionWrite(originalPosition);
                    }

                    if (entriesSent >= maxEntriesToSend || acksSent > 10 ||
                        _sendStream.BitPosition >= maxBitsToSerialize)
                        break;
                }

                if (removeSent)
                {
                    for (var i = 0; i < removedIndices.Value.Count; i++)
                        toSend.RemoveAt(removedIndices.Value[i] - i);
                }
            }

            _totalBitsSent += _sendStream.BitPosition;
            _server.Callback.SendStateSync(_sendStream, State.EndpointId, false);
            return acksSent > 0;
        }

        private void SendReplicationCreate(IMyReplicable obj)
        {
            if (_server.MReplicationPaused)
            {
                PausedReplicables.Add(obj);
                return;
            }

            var typeId = _server.GetTypeIdByType(obj.GetType());
            var networkId = _server.GetNetworkIdByObject(obj);
            var stateGroups = _server.MReplicableGroups[obj];
            _sendStream.ResetWrite();
            _sendStream.WriteTypeId(typeId);
            _sendStream.WriteNetworkId(networkId);
            var streamer = obj as IMyStreamableReplicable;
            bool isStreaming = streamer != null && streamer.NeedsToBeStreamed;
            if (streamer != null && !streamer.NeedsToBeStreamed)
                _sendStream.WriteByte((byte) (stateGroups.Count - 1));
            else
                _sendStream.WriteByte((byte) stateGroups.Count);

            foreach (var t in stateGroups)
                if (isStreaming || t.GroupType != StateGroupEnum.Streaming)
                    _sendStream.WriteNetworkId(_server.GetNetworkIdByObject(t));

            if (isStreaming)
            {
                Replicables[obj].IsStreaming = true;
                _totalBitsSent += _sendStream.BitPosition;
                _server.Callback.SendReplicationCreateStreamed(_sendStream, _clientEndpoint);
                return;
            }

            obj.OnSave(_sendStream);
            _totalBitsSent += _sendStream.BitPosition;
            _server.Callback.SendReplicationCreate(_sendStream, _clientEndpoint);
        }

        private void SendReplicationIslandDone(byte islandIndex)
        {
            _sendStream.ResetWrite();
            _sendStream.WriteByte(islandIndex);
            _totalBitsSent += _sendStream.BitPosition;
            _server.Callback.SendReplicationIslandDone(_sendStream, _clientEndpoint);
        }

        private void SendStreamingEntries(ICollection<MyStateDataEntry> streaming, out int sent)
        {
            sent = 0;
            MyStateDataEntry firstState = streaming.First();
            SendStreamingEntry(firstState);
            sent++;
            if (GeneratedMethods.ClientDataReplicableToIslandTryGetValue(ReplicableToIsland,
                (IMyStreamableReplicable) firstState.Group.Owner, out var islandData))
            {
                foreach (IMyStreamableReplicable myStreamableReplicable in islandData.Replicables)
                {
                    if (StateGroups.TryGetValue(myStreamableReplicable.GetStreamingStateGroup(),
                            out var stateGroup) && firstState != stateGroup &&
                        streaming.Contains(stateGroup))
                    {
                        SendStreamingEntry(stateGroup);
                        sent++;
                    }
                }
            }

            _stats.StreamingPacketsSent = sent;
        }


        private void SendStreamingEntry(MyStateDataEntry entry)
        {
            int maxValue = int.MaxValue;
            MyTimeSpan timestamp = WritePacketHeader(true);
            int bitPosition = _sendStream.BitPosition;
            _sendStream.WriteNetworkId(entry.GroupId);
            using (new StateGroupLock(entry.Group))
                entry.Group.Serialize(_sendStream, State.EndpointId, timestamp, StateSyncPacketId, maxValue, ClientCachedData);
            if (entry.Group.IsProcessingForClient(State.EndpointId))
            {
                return;
            }

            int bitCount = _sendStream.BitPosition - bitPosition;

            if (_bandwidthCounter.Add(entry.Group.GroupType, bitCount))
                entry.LastSyncedFrame = _server.MServerFrame;
            else
                entry.Group.OnAck(State, StateSyncPacketId, false);

            _totalBitsSent += _sendStream.BitPosition;
            _server.Callback.SendStateSync(_sendStream, _clientEndpoint, true);
            IMyReplicable owner = entry.Group.Owner;
            if (owner == null)
                return;
            using (var list = CollectionCache<List<IMyReplicable>, IMyReplicable>.Borrow())
            {
                using (var repBase = _server.BorrowReplicables(true))
                    repBase.Value.GetAllChildren(owner, list.Value);
                foreach (IMyReplicable replicable in list.Value)
                {
                    if (HasReplicable(replicable))
                        continue;
                    AddForClient(replicable,
                        owner.GetPriority(GeneratedMethods.AllocateClientInfo(InternalClientData), true), false);
                }
            }
        }

        private void SendReplicationDestroy(IMyReplicable obj)
        {
            if (_server.MReplicationPaused && PausedReplicables.Remove(obj))
                return;

            _sendStream.ResetWrite();
            _sendStream.WriteNetworkId(_server.GetNetworkIdByObject(obj));

            _totalBitsSent += _sendStream.BitPosition;
            _server.Callback.SendReplicationDestroy(_sendStream, _clientEndpoint);
        }

        #endregion

        private void AddClientReplicable(IMyReplicable replicable,
            float priority, bool force)
        {
            Replicables.Add(replicable, new MyReplicableClientData
            {
                Priority = priority
            });
            foreach (IMyStateGroup group in _server.MReplicableGroups[replicable])
            {
                var netid = _server.GetNetworkIdByObject(group);
                if (group.GroupType != StateGroupEnum.Streaming || ((IMyStreamableReplicable) replicable).NeedsToBeStreamed)
                {
                    StateGroups.Add(group, new MyStateDataEntry(netid, group));
                    DirtyGroups.Add(group);
                    group.CreateClientData(State);
                    if (force)
                        group.ForceSend(State);
                }
            }
        }

        private void RemoveClientReplicable(IMyReplicable replicable)
        {
            if (!_server.MReplicableGroups.TryGetValue(replicable, out var stateGroup))
                return;

            foreach (var group in stateGroup)
            {
                group.DestroyClientData(State);
                StateGroups.Remove(group);
                DirtyGroups.Remove(group);
            }

            Replicables.Remove(replicable);
        }

        public void Dispose()
        {
            _sendStream?.Dispose();
        }
    }
}