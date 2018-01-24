using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using NLog;
using Torch.Utils;
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
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly MyTimeSpan _maximumPacketGap = MyTimeSpan.FromSeconds(0.40000000596046448);

        public ThreadedReplicationServer(IReplicationServerCallback callback, EndpointId? localClientEndpoint,
            bool usePlayoutDelayBuffer) : base(callback, localClientEndpoint, usePlayoutDelayBuffer)
        {
        }


        public void RefreshReplicable(IMyReplicable rep)
        {
            foreach (var client in _clientStates)
                client.Value.RefreshReplicable(this, rep, client.Key, false);
        }

        private MyTimeSpan WritePacketHeader(ClientData clientData, bool streaming)
        {
            clientData.LastStateSyncTimeStamp = MServerTimeStamp;
            if (!streaming)
            {
                clientData.StateSyncPacketId += 1;
            }

            if (clientData.StartingServerTimeStamp == MyTimeSpan.Zero)
            {
                clientData.StartingServerTimeStamp = MServerTimeStamp;
            }

            MyTimeSpan result = MServerTimeStamp - clientData.StartingServerTimeStamp;
            SendStream.ResetWrite();
            SendStream.WriteBool(streaming);
            SendStream.WriteByte(streaming ? (byte) 0 : clientData.StateSyncPacketId, 8);
            clientData.ClientStats.Write(SendStream);
            clientData.ClientStats.Reset();
            SendStream.WriteDouble(result.Milliseconds);
            SendStream.WriteDouble(clientData.LastClientRealtime.Milliseconds);
            clientData.LastClientRealtime = MyTimeSpan.FromMilliseconds(-1.0);
            MCallback.SendCustomState(SendStream);
            return result;
        }

        private void SendEmptyStateSync(ClientData clientData)
        {
            WritePacketHeader(clientData, false);
            MCallback.SendStateSync(SendStream, clientData.State.EndpointId, false);
        }

        private bool SendStateSync(ClientData clientData)
        {
            MyTimeSpan timestamp = WritePacketHeader(clientData, false);
            int mtusize = MCallback.GetMTUSize(clientData.State.EndpointId);
            int num = 8 * (mtusize - 8 - 1);
            int num2 = mtusize / 8;
            int num3 = 0;
            MLimits.Clear();
            int num4 = 0;
            foreach (MyStateDataEntry myStateDataEntry in MTmpSortEntries)
            {
                int bitPosition = SendStream.BitPosition;
                SendStream.WriteNetworkId(myStateDataEntry.GroupId);
                myStateDataEntry.Group.Serialize(SendStream, clientData.State.EndpointId, timestamp,
                    clientData.StateSyncPacketId, num, clientData.ClientCachedData);
                int num5 = SendStream.BitPosition - bitPosition;
                if (num5 > 0 && SendStream.BitPosition <= num &&
                    MLimits.Add(myStateDataEntry.Group.GroupType, num5))
                {
                    clientData.PendingStateSyncAcks[clientData.StateSyncPacketId].Add(myStateDataEntry.Group);
                    num3++;
                    myStateDataEntry.LastSyncedFrame = MServerFrame;
                    MTmpSentEntries.Add(myStateDataEntry);
                }
                else
                {
                    num4++;
                    myStateDataEntry.Group.OnAck(clientData.State, clientData.StateSyncPacketId, false);
                    SendStream.SetBitPositionWrite(bitPosition);
                }

                if (num3 >= num2 || SendStream.BitPosition >= num || num4 > 10)
                {
                    break;
                }
            }

            MCallback.SendStateSync(SendStream, clientData.State.EndpointId, false);
            return num4 > 0;
        }

        private void SendReplicationCreate(IMyReplicable obj, ClientData clientData, Endpoint clientEndpoint)
        {
            if (MReplicationPaused)
            {
                clientData.PausedReplicables.Add(obj);
                return;
            }

            TypeId typeIdByType = GetTypeIdByType(obj.GetType());
            NetworkId networkIdByObject = GetNetworkIdByObject(obj);
            List<IMyStateGroup> list = MReplicableGroups[obj];
            SendStream.ResetWrite();
            SendStream.WriteTypeId(typeIdByType);
            SendStream.WriteNetworkId(networkIdByObject);
            IMyStreamableReplicable myStreamableReplicable = obj as IMyStreamableReplicable;
            bool flag = myStreamableReplicable != null && myStreamableReplicable.NeedsToBeStreamed;
            if (myStreamableReplicable != null && !myStreamableReplicable.NeedsToBeStreamed)
            {
                SendStream.WriteByte((byte) (list.Count - 1), 8);
            }
            else
            {
                SendStream.WriteByte((byte) list.Count, 8);
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (flag || list[i].GroupType != StateGroupEnum.Streaming)
                {
                    SendStream.WriteNetworkId(GetNetworkIdByObject(list[i]));
                }
            }

            if (flag)
            {
                clientData.Replicables[obj].IsStreaming = true;
                MCallback.SendReplicationCreateStreamed(SendStream, clientEndpoint);
                return;
            }

            obj.OnSave(SendStream);
            MCallback.SendReplicationCreate(SendStream, clientEndpoint);
        }

        public override void SendUpdate()
        {
            MServerTimeStamp = MCallback.GetUpdateTime();
            MServerFrame += 1L;
            if (_clientStates.Count == 0)
            {
                return;
            }
            
            Stats_ObjectsRefreshed = 0;
            Stats_ObjectsSent = 0;
            MPriorityUpdates.ApplyChanges();
            if (MPriorityUpdates.Count > 0)
            {
                _tmpHash.Clear();
                foreach (IMyReplicable myReplicable in MPriorityUpdates)
                {
                    if (!myReplicable.HasToBeChild)
                    {
                        RefreshReplicable(myReplicable);
                    }

                    MPriorityUpdates.Remove(myReplicable, false);
                    _tmpHash.Add(myReplicable);
                }

                foreach (KeyValuePair<Endpoint, ClientData> keyValuePair in _clientStates)
                {
                    if (keyValuePair.Value.IsReady)
                    {
                        keyValuePair.Value.SendStateSync(this, _tmpHash);
                    }
                }

                MPriorityUpdates.ApplyRemovals();
                return;
            }

            using (var enumerator3 = _clientStates.GetEnumerator())
            {
                while (enumerator3.MoveNext())
                {
                    KeyValuePair<Endpoint, ClientData> client = enumerator3.Current;
                    if (client.Value.IsReady)
                    {
                        client.Value._layerUpdateHash.Clear();
                        client.Value._toDeleteHash.Clear();
                        client.Value._lastLayerAdditions.Clear();
                        IMyReplicable controlledReplicable = client.Value.State.ControlledReplicable;
                        IMyReplicable characterReplicable = client.Value.State.CharacterReplicable;
                        int num = client.Value.UpdateLayers.Length;
                        UpdateLayer[] updateLayers = client.Value.UpdateLayers;
                        UpdateLayer updateLayer = updateLayers[client.Value.UpdateLayers.Length - 1];
                        foreach (UpdateLayer layer in client.Value.UpdateLayers)
                        {
                            num--;
                            Vector3D min = client.Value.State.Position -
                                           new Vector3D(layer.Descriptor.Radius);
                            BoundingBoxD aabb = new BoundingBoxD(min,
                                client.Value.State.Position + new Vector3D(layer.Descriptor.Radius));
                            MReplicables.GetReplicablesInBox(aabb, layer.Updater.List);
                            _log.Info($"Client {client.Key} has {layer.Updater.List.Count} replicables nearby on layer {num}");
                            foreach (IMyReplicable item in layer.Replicables)
                            {
                                if (!client.Value._layerUpdateHash.Contains(item))
                                {
                                    client.Value._toDeleteHash.Add(item);
                                }
                            }

                            layer.Replicables.Clear();
                            foreach (IMyReplicable rep in layer.Updater.List)
                            {
                                client.Value.AddReplicableToLayer(this, rep, layer);
                            }
                            _log.Info($"Client {client.Key} has {layer.Updater.List.Count} replicables nearby on layer {num} after children");

                            KeyValuePair<Endpoint, ClientData> client10 = client;
                            foreach (KeyValuePair<IMyReplicable, byte> keyValuePair2 in client10.Value
                                .PermanentReplicables)
                            {
                                if (keyValuePair2.Value == num)
                                {
                                    client.Value.AddReplicableToLayer(this, keyValuePair2.Key, layer);
                                }
                            }

                            if (num == 0)
                            {
                                if (controlledReplicable != null)
                                {
                                    client.Value.AddReplicableToLayer(this, controlledReplicable, layer);
                                    client.Value.AddReplicableToLayer(this, characterReplicable, layer);
                                }

                                foreach (IMyReplicable rep2 in client.Value._lastLayerAdditions)
                                {
                                    client.Value.AddReplicableToLayer(this, rep2, layer);
                                }
                            }

                            layer.Updater.List.Clear();
                            layer.Sender.List.Clear();
                            foreach (IMyReplicable item2 in layer.Replicables)
                            {
                                layer.Updater.List.Add(item2);
                                layer.Sender.List.Add(item2);
                            }

                            layer.Updater.Update();
                            layer.Updater.Iterate((rep) =>
                            {
                                client.Value.RefreshReplicable(this, rep, client.Key, true);
                            });
                        }

                        foreach (IMyReplicable myReplicable2 in client.Value._toDeleteHash)
                        {
                            KeyValuePair<Endpoint, ClientData> client11 = client;
                            if (client11.Value.HasReplicable(myReplicable2))
                            {
                                IMyReplicable replicable2 = myReplicable2;
                                Endpoint key = client.Key;
                                client.Value.RemoveForClient(this, replicable2, key, true);
                            }
                        }

                        client.Value._toDeleteHash.Clear();
                    }
                }
            }

            foreach (KeyValuePair<Endpoint, ClientData> keyValuePair3 in _clientStates)
            {
                if (keyValuePair3.Value.IsReady)
                {
                    _tmpHash.Clear();
                    foreach (UpdateLayer updateLayer2 in keyValuePair3.Value.UpdateLayers)
                    {
                        updateLayer2.Sender.Update();
                        updateLayer2.Sender.Iterate(delegate(IMyReplicable x)
                        {
                            using (_tmp)
                            {
                                _tmpHash.Add(x);
                            }
                        });
                    }

                    if (_tmpHash.Count > 0)
                    {
                        keyValuePair3.Value.SendStateSync(this, _tmpHash);
                    }
                }
            }

            foreach (KeyValuePair<Endpoint, ClientData> keyValuePair4 in _clientStates)
            {
                if (MServerTimeStamp > keyValuePair4.Value.LastStateSyncTimeStamp + _maximumPacketGap)
                {
                    SendEmptyStateSync(keyValuePair4.Value);
                }

                if (MServerTimeStamp > keyValuePair4.Value.LastReceivedTimeStamp + _maximumPacketGap)
                {
                    keyValuePair4.Value.State.ResetControlledEntityControls();
                }
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

        public class ClientData
        {
            #region Accessors

            public const string InternalTypeName = "VRage.Network.MyReplicationServer+ClientData, VRage";
#pragma warning disable 649
            [ReflectedGetter(Name = "State", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyClientStateBase> _stateGetter;

            [ReflectedGetter(Name = "EventQueue", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyPacketQueue> _eventQueueGetter;

            [ReflectedGetter(Name = "PermanentReplicables", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, Dictionary<IMyReplicable, byte>> _permanentReplicablesGetter;

            [ReflectedGetter(Name = "Replicables", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyConcurrentDictionary<IMyReplicable, MyReplicableClientData>>
                _replicablesGetter;

            [ReflectedGetter(Name = "BlockedReplicables", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, object> _blockedReplicablesGetter;

            [ReflectedGetter(Name = "StateGroups", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyConcurrentDictionary<IMyStateGroup, MyStateDataEntry>>
                _stateGroupsGetter;

            [ReflectedGetter(Name = "DirtyGroups", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyConcurrentHashSet<IMyStateGroup>> _dirtyGroupsGetter;

            [ReflectedGetter(Name = "DirtyGroupsToRemove", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, List<IMyStateGroup>> _dirtyGroupsToRemoveGetter;

            [ReflectedGetter(Name = "PausedReplicables", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, HashSet<IMyReplicable>> _pausedReplicablesGetter;

            [ReflectedGetter(Name = "ClientCachedData", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, HashSet<string>> _clientCachedDataGetter;

            [ReflectedGetter(Name = "StateSyncPacketId", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, byte> _stateSyncPacketIdGetter;

            [ReflectedSetter(Name = "StateSyncPacketId", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, byte> _stateSyncPacketIdSetter;

            [ReflectedGetter(Name = "LastReceivedAckId", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, byte> _lastReceivedAckIdGetter;

            [ReflectedSetter(Name = "LastReceivedAckId", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, byte> _lastReceivedAckIdSetter;

            [ReflectedGetter(Name = "LastStateSyncPacketId", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, byte> _lastStateSyncPacketIdGetter;

            [ReflectedSetter(Name = "LastStateSyncPacketId", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, byte> _lastStateSyncPacketIdSetter;

            [ReflectedGetter(Name = "LastClientPacketId", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, byte> _lastClientPacketIdGetter;

            [ReflectedSetter(Name = "LastClientPacketId", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, byte> _lastClientPacketIdSetter;

            [ReflectedGetter(Name = "LastClientRealtime", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyTimeSpan> _lastClientRealtimeGetter;

            [ReflectedSetter(Name = "LastClientRealtime", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, MyTimeSpan> _lastClientRealtimeSetter;

            [ReflectedGetter(Name = "WaitingForReset", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, bool> _waitingForResetGetter;

            [ReflectedSetter(Name = "WaitingForReset", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, bool> _waitingForResetSetter;

            [ReflectedGetter(Name = "IsReady", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, bool> _isReadyGetter;

            [ReflectedSetter(Name = "IsReady", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, bool> _isReadySetter;

            [ReflectedGetter(Name = "LastProcessedClientPacketId", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, byte> _lastProcessedClientPacketIdGetter;

            [ReflectedSetter(Name = "LastProcessedClientPacketId", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, byte> _lastProcessedClientPacketIdSetter;

            [ReflectedGetter(Name = "StartingServerTimeStamp", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyTimeSpan> _startingServerTimeStampGetter;

            [ReflectedSetter(Name = "StartingServerTimeStamp", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, MyTimeSpan> _startingServerTimeStampSetter;

            [ReflectedGetter(Name = "LastReceivedTimeStamp", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyTimeSpan> _lastReceivedTimeStampGetter;

            [ReflectedSetter(Name = "LastReceivedTimeStamp", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, MyTimeSpan> _lastReceivedTimeStampSetter;

            [ReflectedGetter(Name = "PriorityMultiplier", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, float> _priorityMultiplierGetter;

            [ReflectedSetter(Name = "PriorityMultiplier", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, float> _priorityMultiplierSetter;

            [ReflectedGetter(Name = "UpdateLayers", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, UpdateLayer[]> _updateLayersGetter;

            [ReflectedSetter(Name = "UpdateLayers", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, UpdateLayer[]> _updateLayersSetter;

            [ReflectedGetter(Name = "PendingStateSyncAcks", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, List<IMyStateGroup>[]> _pendingStateSyncAcksGetter;

            [ReflectedGetter(Name = "ProcessedPacket", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, bool> _processedPacketGetter;

            [ReflectedSetter(Name = "ProcessedPacket", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, bool> _processedPacketSetter;

            [ReflectedGetter(Name = "LastStateSyncTimeStamp", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyTimeSpan> _lastStateSyncTimeStampGetter;

            [ReflectedSetter(Name = "LastStateSyncTimeStamp", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, MyTimeSpan> _lastStateSyncTimeStampSetter;

            [ReflectedGetter(Name = "ClientTracker", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyPacketTracker> _clientTrackerGetter;

            [ReflectedSetter(Name = "ClientTracker", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, MyPacketTracker> _clientTrackerSetter;

            [ReflectedGetter(Name = "ClientStats", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, MyPacketStatistics> _clientStatsGetter;

            [ReflectedSetter(Name = "ClientStats", TypeName = InternalTypeName)]
            private static readonly Action<TClientData, MyPacketStatistics> _clientStatsSetter;

            [ReflectedGetter(Name = "Islands", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, object> _islandsGetter;

            [ReflectedGetter(Name = "ReplicableToIsland", TypeName = InternalTypeName)]
            private static readonly Func<TClientData, IDictionary> _replicableToIslandGetter;

            [ReflectedMethod(Name = "Remove", OverrideTypes = new[] {typeof(IMyReplicable)},
                TypeName =
                    "VRage.Collections.MyConcurrentDictionary`2[[VRage.Network.IMyReplicable, VRage], [VRage.Network.MyReplicationServer+MyDestroyBlocker, VRage]], VRage.Library")]
            private static readonly Func<object, IMyReplicable, bool> _blockedReplicablesRemove;
#pragma warning disable 649

            public TClientData InternalClientData;


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


            public UpdateLayer[] UpdateLayers
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

            private readonly CacheList<MyStateDataEntry> _mTmpSortEntries = new CacheList<MyStateDataEntry>();
            private readonly CacheList<MyStateDataEntry> _mTmpStreamingEntries = new CacheList<MyStateDataEntry>();
            private readonly CacheList<MyStateDataEntry> _mTmpSentEntries = new CacheList<MyStateDataEntry>();

            public readonly HashSet<IMyReplicable> _layerUpdateHash = new HashSet<IMyReplicable>();
            public readonly HashSet<IMyReplicable> _toDeleteHash = new HashSet<IMyReplicable>();
            public readonly HashSet<IMyReplicable> _lastLayerAdditions = new HashSet<IMyReplicable>();

            private readonly ThreadedReplicationServer repServ;

            public ClientData(ThreadedReplicationServer server)
            {
                repServ = server;
            }


            public void FillEntries(ThreadedReplicationServer repServ, HashSet<IMyReplicable> replicables)
            {
                foreach (IMyStateGroup myStateGroup in DirtyGroups)
                {
                    if (replicables.Contains(myStateGroup.Owner.GetParent() ?? myStateGroup.Owner) &&
                        Replicables.ContainsKey(myStateGroup.Owner) &&
                        (Replicables[myStateGroup.Owner].HasActiveStateSync ||
                         myStateGroup.GroupType == StateGroupEnum.Streaming))
                    {
                        MyStateDataEntry myStateDataEntry = StateGroups[myStateGroup];
                        myStateDataEntry.Priority = myStateDataEntry.Group.GetGroupPriority(
                            (int) (repServ.MServerFrame - myStateDataEntry.LastSyncedFrame),
                            GeneratedMethods.AllocateClientInfo(InternalClientData));
                        if (myStateDataEntry.Priority > 0f &&
                            !myStateDataEntry.Group.IsProcessingForClient(State.EndpointId))
                        {
                            if (myStateDataEntry.Group.GroupType == StateGroupEnum.Streaming)
                            {
                                _mTmpStreamingEntries.Add(myStateDataEntry);
                                repServ.Stats_ObjectsSent++;
                            }
                            else
                            {
                                _mTmpSortEntries.Add(myStateDataEntry);
                                repServ.Stats_ObjectsSent++;
                            }
                        }

                        if (!myStateGroup.IsStillDirty(State.EndpointId))
                        {
                            DirtyGroupsToRemove.Add(myStateGroup);
                        }
                    }
                }

                _mTmpSortEntries.Sort(MyStateDataEntryComparer.Instance);
            }

            public void SendStateSync(ThreadedReplicationServer repServ, HashSet<IMyReplicable> replicables)
            {
                if (StateGroups.Count == 0)
                {
                    return;
                }

                if (DirtyGroups.Count == 0)
                {
                    return;
                }

                Endpoint endpointId = State.EndpointId;
                EventQueue.Send(int.MaxValue);
                using (_mTmpStreamingEntries)
                {
                    using (_mTmpSortEntries)
                    {
                        FillEntries(repServ, replicables);
                        byte b = (byte) (LastReceivedAckId - 6);
                        byte b2 = (byte) (StateSyncPacketId + 1);
                        if (WaitingForReset || b2 == b)
                        {
                            WaitingForReset = true;
                            return;
                        }

                        int num = 0;
                        while (repServ.SendStateSync(this) && num <= 7)
                        {
                            foreach (MyStateDataEntry item in _mTmpSentEntries)
                            {
                                _mTmpSortEntries.Remove(item);
                            }

                            num++;
                            _mTmpSentEntries.Clear();
                        }

                        _mTmpSentEntries.Clear();
                        if (_mTmpStreamingEntries.Count > 0)
                        {
                            _mTmpStreamingEntries.Sort(MyStateDataEntryComparer.Instance);
                            SendStreamingEntries(repServ);
                        }
                    }
                }

                foreach (IMyStateGroup value in DirtyGroupsToRemove)
                {
                    DirtyGroups.Remove(value);
                }

                DirtyGroupsToRemove.Clear();
                using (var islandEnumerator = GeneratedMethods.ClientDataIslandsEnumerator(Islands))
                {
                    while (islandEnumerator.MoveNext())
                    {
                        var island = islandEnumerator.Current;
                        bool flag = true;
                        foreach (IMyStreamableReplicable myStreamableReplicable in island.Replicables)
                        {
                            if (DirtyGroups.Contains(myStreamableReplicable.GetStreamingStateGroup()))
                            {
                                flag = false;
                                break;
                            }
                        }

                        if (flag)
                        {
                            SendReplicationIslandDone(repServ, island.Index, endpointId);
                            GeneratedMethods.RemoveCachedIsland(InternalClientData, island);
                        }
                    }
                }

                GeneratedMethods.ClientDataIslandsApplyRemovals(Islands);
            }

            private void SendReplicationIslandDone(ThreadedReplicationServer repServ, byte islandIndex,
                Endpoint clientEndpoint)
            {
                repServ.SendStream.ResetWrite();
                repServ.SendStream.WriteByte(islandIndex, 8);
                repServ.MCallback.SendReplicationIslandDone(repServ.SendStream, clientEndpoint);
            }

            public void AddReplicableToLayer(ThreadedReplicationServer repServ, IMyReplicable rep,
                UpdateLayer layer)
            {
                if (!_layerUpdateHash.Contains(rep))
                {
                    AddReplicableToLayerSingle(rep, layer);
                    HashSet<IMyReplicable> physicalDependencies =
                        rep.GetPhysicalDependencies(repServ.MServerTimeStamp, repServ.MReplicables);
                    if (physicalDependencies != null)
                    {
                        foreach (IMyReplicable rep2 in physicalDependencies)
                        {
                            AddReplicableToLayerSingle(rep2, layer);
                        }
                    }
                }
            }

            public void AddReplicableToLayerSingle(IMyReplicable rep, UpdateLayer layer)
            {
                if (!_layerUpdateHash.Contains(rep))
                {
                    layer.Replicables.Add(rep);
                    _layerUpdateHash.Add(rep);
                    _toDeleteHash.Remove(rep);
                    HashSet<IMyReplicable> dependencies = rep.GetDependencies();
                    if (dependencies != null)
                    {
                        foreach (IMyReplicable item in dependencies)
                        {
                            _lastLayerAdditions.Add(item);
                        }
                    }
                }
            }

            private void SendStreamingEntries(ThreadedReplicationServer repServ)
            {
                MyStateDataEntry myStateDataEntry = _mTmpStreamingEntries.FirstOrDefault<MyStateDataEntry>();
                SendStreamingEntry(repServ, myStateDataEntry);
                GeneratedMethods.IslandData islandData;
                if (GeneratedMethods.ClientDataReplicableToIslandTryGetValue(ReplicableToIsland,
                    myStateDataEntry.Group.Owner as IMyStreamableReplicable,
                    out islandData))
                {
                    foreach (IMyStreamableReplicable myStreamableReplicable in islandData.Replicables)
                    {
                        MyStateDataEntry myStateDataEntry2;
                        if (StateGroups.TryGetValue(myStreamableReplicable.GetStreamingStateGroup(),
                                out myStateDataEntry2) && myStateDataEntry != myStateDataEntry2 &&
                            _mTmpStreamingEntries.Contains(myStateDataEntry2))
                        {
                            SendStreamingEntry(repServ, myStateDataEntry2);
                        }
                    }
                }
            }

            private readonly CacheList<IMyReplicable> _mTmp = new CacheList<IMyReplicable>();
            
            private void SendStreamingEntry(ThreadedReplicationServer repServ, MyStateDataEntry entry)
            {
                Endpoint endpointId = State.EndpointId;
                int maxValue = int.MaxValue;
                MyTimeSpan timestamp = repServ.WritePacketHeader(this, true);
                int bitPosition = repServ.SendStream.BitPosition;
                repServ.SendStream.WriteNetworkId(entry.GroupId);
                entry.Group.Serialize(repServ.SendStream, State.EndpointId, timestamp, StateSyncPacketId, maxValue,
                    ClientCachedData);
                if (entry.Group.IsProcessingForClient(State.EndpointId))
                {
                    return;
                }

                int bitCount = repServ.SendStream.BitPosition - bitPosition;

                if (repServ.MLimits.Add(entry.Group.GroupType, bitCount))
                {
                    entry.LastSyncedFrame = repServ.MServerFrame;
                }
                else
                {
                    entry.Group.OnAck(State, StateSyncPacketId, false);
                }

                repServ.MCallback.SendStateSync(repServ.SendStream, endpointId, true);
                IMyReplicable owner = entry.Group.Owner;
                if (owner != null)
                {
                    using (_mTmp)
                    {
                        repServ.MReplicables.GetAllChildren(owner, _mTmp);
                        foreach (IMyReplicable replicable in _mTmp)
                        {
                            if (!HasReplicable(replicable))
                            {
                                AddForClient(repServ, replicable, endpointId,
                                    owner.GetPriority(GeneratedMethods.AllocateClientInfo(InternalClientData), true),
                                    false, false);
                            }
                        }
                    }
                }
            }

            public void RefreshReplicable(ThreadedReplicationServer repServ, IMyReplicable replicable,
                Endpoint endPoint, bool checkDependencies = false)
            {
                MyTimeSpan updateTime = repServ.MCallback.GetUpdateTime();
                if (!IsReady)
                {
                    return;
                }

                MyReplicableClientData myReplicableClientData;
                bool flag = Replicables.TryGetValue(replicable, out myReplicableClientData);
                float priority =
                    replicable.GetPriority(GeneratedMethods.AllocateClientInfo(InternalClientData), false);
                if (flag)
                {
                    myReplicableClientData.Priority = priority;
                }

                bool flag2 = priority > 0f;
                if (flag2 && !flag)
                {
                    AddForClient(repServ, replicable, endPoint, priority, false, checkDependencies);
                }
                else if (flag)
                {
                    myReplicableClientData.UpdateSleep(flag2, updateTime);
                    if (myReplicableClientData.ShouldRemove(updateTime, repServ.MaxSleepTime))
                    {
                        RemoveForClient(repServ, replicable, endPoint, true);
                    }
                }

                repServ.Stats_ObjectsRefreshed++;
            }

            private void AddClientReplicable(ThreadedReplicationServer repServ, IMyReplicable replicable,
                float priority, bool force)
            {
                Replicables.Add(replicable, new MyReplicableClientData
                {
                    Priority = priority
                });
                foreach (IMyStateGroup myStateGroup in repServ.MReplicableGroups[replicable])
                {
                    NetworkId networkIdByObject = repServ.GetNetworkIdByObject(myStateGroup);
                    if (myStateGroup.GroupType != StateGroupEnum.Streaming ||
                        (replicable as IMyStreamableReplicable).NeedsToBeStreamed)
                    {
                        StateGroups.Add(myStateGroup, new MyStateDataEntry(networkIdByObject, myStateGroup));
                        DirtyGroups.Add(myStateGroup);
                        myStateGroup.CreateClientData(State);
                        if (force)
                        {
                            myStateGroup.ForceSend(State);
                        }
                    }
                }
            }

            public void AddForClient(ThreadedReplicationServer repServ, IMyReplicable replicable,
                Endpoint clientEndpoint, float priority, bool force, bool addDependencies = false)
            {
                if (!replicable.IsReadyForReplication)
                {
                    return;
                }

                if (HasReplicable(replicable))
                {
                    return;
                }

                AddClientReplicable(repServ, replicable, priority, force);
                repServ.SendReplicationCreate(replicable, this, clientEndpoint);
                IMyStreamableReplicable myStreamableReplicable = replicable as IMyStreamableReplicable;
                if (myStreamableReplicable == null)
                {
                    foreach (IMyReplicable replicable2 in repServ.MReplicables.GetChildren(replicable))
                    {
                        AddForClient(repServ, replicable2, clientEndpoint, priority, force, false);
                    }
                }

                HashSet<IMyReplicable> physicalDependencies =
                    replicable.GetPhysicalDependencies(repServ.MServerTimeStamp, repServ.MReplicables);
                if (physicalDependencies != null && physicalDependencies.Count > 0)
                {
                    if (myStreamableReplicable == null ||
                        !ReplicableToIsland.Contains(myStreamableReplicable))
                    {
                        GeneratedMethods.CreateNewCachedIsland(InternalClientData, replicable, physicalDependencies,
                            repServ.MServerTimeStamp);
                    }

                    if (addDependencies)
                    {
                        foreach (IMyReplicable replicable3 in physicalDependencies)
                        {
                            AddForClient(repServ, replicable3, clientEndpoint, priority, force, false);
                        }
                    }
                }
            }

            public void RemoveForClient(ThreadedReplicationServer repServ, IMyReplicable replicable,
                Endpoint clientEndpoint, bool sendDestroyToClient)
            {
                using (repServ._tmp)
                {
                    repServ.MReplicables.RefreshChildrenHierarchy(replicable);
                    repServ.MReplicables.GetAllChildren(replicable, repServ._tmp);
                    repServ._tmp.Add(replicable);
                    foreach (IMyReplicable myReplicable in repServ._tmp)
                    {
                        _blockedReplicablesRemove(BlockedReplicables, myReplicable);
                        if (sendDestroyToClient)
                        {
                            SendReplicationDestroy(repServ, myReplicable, clientEndpoint);
                        }

                        RemoveClientReplicable(repServ, myReplicable);
                    }

                    foreach (UpdateLayer updateLayer in UpdateLayers)
                    {
                        updateLayer.Replicables.Remove(replicable);
                    }
                }
            }

            private void SendReplicationDestroy(ThreadedReplicationServer repServ, IMyReplicable obj,
                Endpoint clientEndpoint)
            {
                if (repServ.MReplicationPaused && PausedReplicables.Remove(obj))
                {
                    return;
                }

                repServ.SendStream.ResetWrite();
                repServ.SendStream.WriteNetworkId(repServ.GetNetworkIdByObject(obj));
                repServ.MCallback.SendReplicationDestroy(repServ.SendStream, clientEndpoint);
            }

            private void RemoveClientReplicable(ThreadedReplicationServer repServ, IMyReplicable replicable)
            {
                if (!repServ.MReplicableGroups.ContainsKey(replicable))
                {
                    return;
                }

                foreach (IMyStateGroup myStateGroup in repServ.MReplicableGroups[replicable])
                {
                    myStateGroup.DestroyClientData(State);
                    StateGroups.Remove(myStateGroup);
                    DirtyGroups.Remove(myStateGroup);
                }

                Replicables.Remove(replicable);
            }
        }

        #region Accessors

        private MyTimeSpan MServerTimeStamp
        {
            get => _serverTimeStampGetter(this);
            set => _serverTimeStampSetter(this, value);
        }

        private long MServerFrame
        {
            get => _serverFrameGetter(this);
            set => _serverFrameSetter(this, value);
        }

        private EndpointId? MLocalClientEndpoint => _localClientEndpointGetter(this);

        private bool MUsePlayoutDelayBuffer => _usePlayoutDelayBufferGetter(this);

        private IReplicationServerCallback MCallback => _callbackGetter(this);

        private Action<BitStream, EndpointId> MEventQueueSender => _eventQueueSenderGetter(this);

        private CacheList<IMyStateGroup> MTmpGroups => _tmpGroupsGetter(this);

        private CacheList<MyStateDataEntry> MTmpSortEntries => _tmpSortEntriesGetter(this);

        private CacheList<MyStateDataEntry> MTmpStreamingEntries => _tmpStreamingEntriesGetter(this);

        private CacheList<MyStateDataEntry> MTmpSentEntries => _tmpSentEntriesGetter(this);

//        private HashSet<IMyReplicable> _toDeleteHash => _toDeleteHashGetter(this);

        private CacheList<IMyReplicable> _tmp => _tmpGetter(this);

        private HashSet<IMyReplicable> _tmpHash => _tmpHashGetter(this);

//        private HashSet<IMyReplicable> _layerUpdateHash => _layerUpdateHashGetter(this);
//
//        private HashSet<IMyReplicable> _lastLayerAdditions => _lastLayerAdditionsGetter(this);

        private CachingHashSet<IMyReplicable> MPostponedDestructionReplicables =>
            _postponedDestructionReplicablesGetter(this);

        private MyBandwidthLimits MLimits => _limitsGetter(this);

        private ConcurrentCachingHashSet<IMyReplicable> MPriorityUpdates => _priorityUpdatesGetter(this);

        /// <summary>
        ///     All replicables on server.
        /// </summary>
        private MyReplicablesBase MReplicables => _replicablesGetter(this);

        /// <summary>
        ///     All replicable state groups.
        /// </summary>
        private Dictionary<IMyReplicable, List<IMyStateGroup>> MReplicableGroups => _replicableGroupsGetter(this);

        /// <summary>
        ///     Network objects and states which are actively replicating to clients.
        /// </summary>
        private IProxyDictionary<Endpoint, ClientData> _clientStates
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
                    o, new ProxyModifier<Endpoint, TClientData, ClientData>(ClientProxyAllocator));
            }
        }

        private bool ClientProxyAllocator(Endpoint ip, TClientData backing, ref ClientData stor)
        {
            if (stor != null)
            {
                stor.InternalClientData = backing;
                return false;
            }

            stor = new ClientData(this) {InternalClientData = backing};
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
            int Count { get; }
        }

        public delegate bool ProxyModifier<in TK, in TB, TV>(TK key, TB value, ref TV proxy);

        private class ProxyDictionary<TK, TV, TB> : IProxyDictionary<TK, TV>
        {
            private readonly ProxyModifier<TK, TB, TV> _proxyModifier;
            private readonly Dictionary<TK, TV> _proxyStorage;
            private readonly Dictionary<TK, TB> _backing;

            private static readonly Func<Dictionary<TK, TB>, int> _backingVersionGet =
                FieldAccess.CreateGetter<Dictionary<TK, TB>, int>(
                    typeof(Dictionary<TK, TB>).GetField("version", BindingFlags.Instance | BindingFlags.NonPublic));

            private readonly ThreadLocal<HashSet<TK>> _tmpRemoval;

            private int _lastVersion;

            public ProxyDictionary(Dictionary<TK, TB> backing, ProxyModifier<TK, TB, TV> proxyAllocator)
            {
                _backing = backing;
                _proxyStorage = new Dictionary<TK, TV>(_backing.Comparer);
                _proxyModifier = proxyAllocator;
                _tmpRemoval = new ThreadLocal<HashSet<TK>>(() => new HashSet<TK>(_backing.Comparer));

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
                foreach (var l in _proxyStorage.Keys)
                    if (!_backing.ContainsKey(l))
                        toRemove.Add(l);
                foreach (var l in toRemove)
                    _backing.Remove(l);
                toRemove.Clear();

                foreach (var l in _backing)
                {
                    var tmp = _proxyStorage.GetValueOrDefault(l.Key);
                    if (_proxyModifier.Invoke(l.Key, l.Value, ref tmp))
                        _proxyStorage[l.Key] = tmp;
                }
            }

            public int Count => _backing.Count;

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