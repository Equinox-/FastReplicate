using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Replication;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage.Library.Collections;
using VRage.Network;
using VRage.Replication;

namespace FastReplicate
{
    public static class Patch
    {
        private static MethodInfo Method(string name)
        {
            return typeof(Patch).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public static void Apply(PatchContext ctx)
        {
            var ctor = typeof(MyMultiplayerServerBase).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] {typeof(MySyncLayer), typeof(EndpointId?)}, null);
            if (ctor == null)
                throw new InvalidOperationException(
                    "Couldn't find MyMultiplayerServerBase.ctor(MySyncLayer, EndpointId?)");

            ctx.GetPattern(ctor).Transpilers
                .Add(Method(nameof(TranspileInitMultiplayer)));
            ctx.GetPattern(_bitStreamClear).Transpilers
                .Add(Method(nameof(TranspileClearBitStream)));
            ctx.GetPattern(_characterReplicableDependencies).Prefixes
                .Add(Method(nameof(CharacterReplicableGetDependencies)));

            GeneratedMethods.Init();
        }

#pragma warning disable 649
        [ReflectedMethodInfo(typeof(BitStream), "Clear")]
        private static readonly MethodInfo _bitStreamClear;

        [ReflectedMethodInfo(null, nameof(IMyReplicable.GetDependencies), TypeName = TypeCharacterReplicable)]
        private static readonly MethodInfo _characterReplicableDependencies;

        [ReflectedFieldInfo(typeof(BitStream), "m_buffer")]
        private static readonly FieldInfo _bitStreamBuffer;

        [ReflectedFieldInfo(typeof(BitStream), "m_bitLength")]
        private static readonly FieldInfo _bitStreamBitLength;

        [ReflectedGetter(Name = "m_dependencies", TypeName = TypeCharacterReplicable)]
        private static readonly Func<MyEntityReplicableBaseEvent<MyCharacter>, HashSet<IMyReplicable>>
            _characterDependenciesGetter;

        [ReflectedGetter(Name = "RadioBroadcaster")]
        private static readonly Func<MyCharacter, MyRadioBroadcaster> _characterBroadcasterGetter;
#pragma warning restore 649

        private static IEnumerable<MsilInstruction> TranspileClearBitStream(IEnumerable<MsilInstruction> ins,
            Func<Type, MsilLocal> __localCreator)
        {
            // discard all of Keen's stuff.

            // compute the start item
            var firstLong = __localCreator(typeof(int));
            yield return new MsilInstruction(OpCodes.Ldarg_1); // fromPosition
            yield return new MsilInstruction(OpCodes.Ldc_I4_6);
            yield return new MsilInstruction(OpCodes.Shr);
            yield return firstLong.AsValueStore();

            // compute the bits to skip
            var bitsToSkip = __localCreator(typeof(int));
            yield return new MsilInstruction(OpCodes.Ldarg_1);
            yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(63);
            yield return new MsilInstruction(OpCodes.And);
            yield return bitsToSkip.AsValueStore();

            // firstLongPtr = m_buffer + (8*firstLong)
            yield return new MsilInstruction(OpCodes.Ldarg_0);
            yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(_bitStreamBuffer);
            yield return firstLong.AsValueLoad();
            yield return new MsilInstruction(OpCodes.Conv_I);
            yield return new MsilInstruction(OpCodes.Ldc_I4_8);
            yield return new MsilInstruction(OpCodes.Mul);
            yield return new MsilInstruction(OpCodes.Add);
            yield return new MsilInstruction(OpCodes.Dup);

            // m_buffer[firstLong] &= ~(-1 << bitsToSkip)
            yield return new MsilInstruction(OpCodes.Ldind_I8); // original
            yield return new MsilInstruction(OpCodes.Ldc_I4_M1);
            yield return new MsilInstruction(OpCodes.Conv_I8);
            yield return bitsToSkip.AsValueLoad(); // original, -1, bitsToSkip
            yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(63);
            yield return new MsilInstruction(OpCodes.And); // original, -1, bitsToSkip & 63
            yield return new MsilInstruction(OpCodes.Shl); // original, -1 << (bitsToSkip & 63)
            yield return new MsilInstruction(OpCodes.Not); // original, ~(-1 << (bitsToSkip & 63))
            yield return new MsilInstruction(OpCodes.And); // original & ~(-1 << (bitsToSkip & 63))
            yield return new MsilInstruction(OpCodes.Stind_I8);


            // firstBulkAddr = m_buffer + (8 * (firstLong + 1))
            yield return new MsilInstruction(OpCodes.Ldarg_0);
            yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(_bitStreamBuffer);
            yield return firstLong.AsValueLoad();
            yield return new MsilInstruction(OpCodes.Ldc_I4_1);
            yield return new MsilInstruction(OpCodes.Add);
            yield return new MsilInstruction(OpCodes.Conv_I);
            yield return new MsilInstruction(OpCodes.Ldc_I4_8);
            yield return new MsilInstruction(OpCodes.Mul);
            yield return new MsilInstruction(OpCodes.Add); // firstBulkAddr, 

            // init value
            yield return new MsilInstruction(OpCodes.Ldc_I4_0);

            // totalBytes = (bitStreamLength / 8)
            yield return new MsilInstruction(OpCodes.Ldarg_0);
            yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(_bitStreamBitLength);
            yield return new MsilInstruction(OpCodes.Ldc_I4_8);
            yield return new MsilInstruction(OpCodes.Div);

            // firstLongByteOffset = (firstLong+1) * 8
            yield return firstLong.AsValueLoad();
            yield return new MsilInstruction(OpCodes.Ldc_I4_1);
            yield return new MsilInstruction(OpCodes.Add);
            yield return new MsilInstruction(OpCodes.Ldc_I4_8);
            yield return new MsilInstruction(OpCodes.Mul);

            // bytesToSet = (bitStreamLength / 8) - ((firstLong + 1) * 8)
            yield return new MsilInstruction(OpCodes.Sub);

            // set memory to init value
            yield return new MsilInstruction(OpCodes.Initblk);
            yield return new MsilInstruction(OpCodes.Ret);
        }

        private static IEnumerable<MsilInstruction> TranspileInitMultiplayer(IEnumerable<MsilInstruction> ins)
        {
            var findCtor = typeof(MyReplicationServer).GetConstructor(new[]
                {typeof(IReplicationServerCallback), typeof(EndpointId?), typeof(bool)});
            if (findCtor == null)
                throw new InvalidOperationException(
                    "Couldn't find MyReplicationServer.ctor(IReplicationServerCallback, EndpointId?, bool)");
            var replaceCtor = typeof(ThreadedReplicationServer).GetConstructor(new[]
                {typeof(IReplicationServerCallback), typeof(EndpointId?), typeof(bool)});
            if (replaceCtor == null)
                throw new InvalidOperationException(
                    "Couldn't find ThreadedReplicationServer.ctor(IReplicationServerCallback, EndpointId?, bool)");
            foreach (var k in ins)
            {
                if (k.Operand is MsilOperandInline<MethodBase> meth && meth.Value == findCtor)
                {
                    var repl = new MsilInstruction(k.OpCode);
                    repl.InlineValue(replaceCtor);
                    foreach (var l in k.Labels)
                        repl.Labels.Add(l);
                    yield return repl;
                    continue;
                }

                yield return k;
            }
        }

        private const string TypeCharacterReplicable = "Sandbox.Game.Replication.MyCharacterReplicable, Sandbox.Game";

        private const string TypeFarBroadcasterReplicable =
            "Sandbox.Game.Replication.MyFarBroadcasterReplicable, Sandbox.Game";

        private static readonly Type _typeCharacterReplicable = Type.GetType(TypeCharacterReplicable, true);
        private static readonly Type _typeFarBroadcasterReplicable = Type.GetType(TypeFarBroadcasterReplicable, true);

        private static readonly ThreadLocal<HashSet<MyDataBroadcaster>> _broadcasterCache =
            new ThreadLocal<HashSet<MyDataBroadcaster>>(() => new HashSet<MyDataBroadcaster>());
        private static readonly ThreadLocal<HashSet<MyDataReceiver>> _receiverCache =
            new ThreadLocal<HashSet<MyDataReceiver>>(() => new HashSet<MyDataReceiver>());

        private static bool CharacterReplicableGetDependencies(MyEntityReplicableBaseEvent<MyCharacter> __instance,
            ref HashSet<IMyReplicable> __result)
        {
            __result = _characterDependenciesGetter(__instance);
            __result.Clear();
            if (!Sync.IsServer)
            {
                return false;
            }

            var playerId = __instance.Instance.GetPlayerIdentityId();
            var allRelayedBroadcasters = _broadcasterCache.Value;
            allRelayedBroadcasters.Clear();

            {
                var receivers = _receiverCache.Value;
                receivers.Clear();
                MyAntennaSystem.Static.GetEntityReceivers(__instance.Instance, ref receivers, playerId);

                foreach (var k in receivers)
                    MyAntennaSystem.Static.GetAllRelayedBroadcasters(k, playerId, false, allRelayedBroadcasters);
            }

            var mine = _characterBroadcasterGetter(__instance.Instance);
            foreach (MyDataBroadcaster others in allRelayedBroadcasters)
            {
                if (mine != others && !others.Closed)
                {
                    var rep = MyExternalReplicable.FindByObject(others);
                    if (rep != null && _typeFarBroadcasterReplicable.IsInstanceOfType(rep))
                    {
                        __result.Add(rep);
                    }
                }
            }

            allRelayedBroadcasters.Clear();
            return false;
        }
    }
}