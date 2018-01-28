using System;
using System.Threading;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Torch.Utils;
using VRage;
using VRage.GameServices;
using VRage.Library.Collections;
using VRage.Library.Utils;
using VRage.Network;
using VRage.Replication;

namespace FastReplicate
{
    public class ConcurrentReplicationCallback : IReplicationServerCallback
    {
#pragma warning disable 649
        public const string TransportLayerType = "Sandbox.Engine.Multiplayer.MyTransportLayer, Sandbox.Game";

        [ReflectedMethod(Name = "SendMessage", TypeName = TransportLayerType)]
        private static readonly Action<object, ByteStream, MyP2PMessageEnum, ulong, int> _transportSendMessage;

        [ReflectedGetter(Name = "m_channel", TypeName = TransportLayerType)]
        private static readonly Func<object, int> _transportChannel;

        [ReflectedGetter(Name = "TransportLayer")]
        private static readonly Func<MySyncLayer, object> _transportLayerGetter;
#pragma warning restore 649

        private readonly ThreadLocal<ByteStream> _sendStreamCache = new ThreadLocal<ByteStream>(() => new ByteStream(65536));

        private static object TransportLayer => _transportLayerGetter(MyMultiplayer.Static.SyncLayer);

        private readonly IReplicationServerCallback _original;
        private readonly int _sizeMtu;
        private readonly int _sizeMtr;

        public ConcurrentReplicationCallback(IReplicationServerCallback original)
        {
            _original = original;
            _sizeMtu = 1200;
            _sizeMtr = 1048576;
        }

        public unsafe void SendMessage(MyMessageId id, BitStream stream, bool reliable, EndpointId endpoint,
            byte index = 0)
        {
            var sendStream = _sendStreamCache.Value;
            var channel = _transportChannel(TransportLayer);

            var mode = reliable ? MyP2PMessageEnum.ReliableWithBuffering : MyP2PMessageEnum.Unreliable;

            if (reliable && stream != null)
            {
                byte b = (byte) (stream.BytePosition / _sizeMtr + 1);
                for (int i = 0; i < b; i++)
                {
                    sendStream.Position = 0L;
                    sendStream.WriteByte((byte) id);
                    sendStream.WriteByte(index);
                    sendStream.WriteByte(i == 0 ? b : (byte) 0);

                    int num = i * (_sizeMtr - 3);
                    int num2 = _sizeMtr - 3;
                    if (num + num2 > stream.BytePosition)
                    {
                        num2 = stream.BytePosition - num;
                    }

                    sendStream.WriteNoAlloc((byte*) stream.DataPointer.ToPointer(), num, num2);

                    _transportSendMessage(TransportLayer, sendStream, mode, endpoint.Value, channel);
                }

                return;
            }

            sendStream.Position = 0L;
            sendStream.WriteByte((byte) id);
            sendStream.WriteByte(index);
            sendStream.WriteByte(1);
            if (stream != null)
                sendStream.WriteNoAlloc((byte*) stream.DataPointer.ToPointer(), 0, stream.BytePosition);

            _transportSendMessage(TransportLayer, sendStream, mode, endpoint.Value, channel);
        }


        public void SendServerData(BitStream stream, Endpoint endpoint)
        {
            SendMessage(MyMessageId.SERVER_DATA, stream, true, endpoint.Id,
                endpoint.Index);
        }


        public void SendReplicationCreate(BitStream stream, Endpoint endpoint)
        {
            SendMessage(MyMessageId.REPLICATION_CREATE, stream, true, endpoint.Id,
                endpoint.Index);
        }


        public void SendReplicationCreateStreamed(BitStream stream, Endpoint endpoint)
        {
            SendMessage(MyMessageId.REPLICATION_STREAM_BEGIN, stream, true, endpoint.Id,
                endpoint.Index);
        }


        public void SendReplicationDestroy(BitStream stream, Endpoint endpoint)
        {
            SendMessage(MyMessageId.REPLICATION_DESTROY, stream, true, endpoint.Id,
                endpoint.Index);
        }


        public void SendReplicationIslandDone(BitStream stream, Endpoint endpoint)
        {
            SendMessage(MyMessageId.REPLICATION_ISLAND_DONE, stream, true, endpoint.Id,
                endpoint.Index);
        }


        public void SendStateSync(BitStream stream, Endpoint endpoint, bool reliable)
        {
            SendMessage(MyMessageId.SERVER_STATE_SYNC, stream, reliable, endpoint.Id,
                endpoint.Index);
        }


        public void SendWorldData(BitStream stream, EndpointId endpoint)
        {
            SendMessage(MyMessageId.WORLD_DATA, stream, true, endpoint, 0);
        }

        public void SendJoinResult(BitStream stream, EndpointId endpoint)
        {
            SendMessage(MyMessageId.JOIN_RESULT, stream, true, endpoint, 0);
        }


        public void SendEvent(BitStream stream, bool reliable, EndpointId endpoint)
        {
            SendMessage(MyMessageId.RPC, stream, reliable, endpoint, 0);
        }

        public void SentClientJoined(BitStream stream, EndpointId endpoint)
        {
            SendMessage(MyMessageId.CLIENT_CONNNECTED, stream, true, endpoint, 0);
        }


        public void SendCustomState(BitStream stream)
        {
            _original.SendCustomState(stream);
        }


        public int GetMTUSize(Endpoint clientId)
        {
            return _original.GetMTUSize(clientId);
        }

        public int GetMTRSize(Endpoint clientId)
        {
            return _original.GetMTRSize(clientId);
        }

        public IMyReplicable GetReplicableByEntityId(long entityId)
        {
            return _original.GetReplicableByEntityId(entityId);
        }

        public void DisconnectClient(ulong clientId)
        {
            _original.DisconnectClient(clientId);
        }

        public MyTimeSpan GetUpdateTime()
        {
            return _original.GetUpdateTime();
        }
    }
}