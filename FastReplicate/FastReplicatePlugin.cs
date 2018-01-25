using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage.Library.Collections;
using VRage.Network;
using VRage.Replication;

namespace FastReplicate
{
    /// <summary>
    /// Plugin FastReplicate
    /// </summary>
    public class FastReplicatePlugin : TorchPluginBase, IWpfPlugin
    {
        public override void Init(ITorchBase torch)
        {
            var mgr = torch.Managers.GetManager<PatchManager>();
            var ctx = mgr.AcquireContext();
            var ctor = typeof(MyMultiplayerServerBase).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] {typeof(MySyncLayer), typeof(EndpointId?)}, null);
            if (ctor == null)
                throw new InvalidOperationException(
                    "Couldn't find MyMultiplayerServerBase.ctor(MySyncLayer, EndpointId?)");

            ctx.GetPattern(ctor).Transpilers
                .Add(typeof(FastReplicatePlugin).GetMethod(nameof(TranspileInitMultiplayer),
                    BindingFlags.Static | BindingFlags.NonPublic));
            ctx.GetPattern(_bitStreamClear).Transpilers
                .Add(typeof(FastReplicatePlugin).GetMethod(nameof(TranspileClearBitStream),
                    BindingFlags.Static | BindingFlags.NonPublic));

            GeneratedMethods.Init();
            mgr.Commit();
        }

        public UserControl GetControl()
        {
            return new FastReplicateUi() {DataContext = this};
        }

        public bool UseFastReplication
        {
            get => ThreadedReplicationServer.UseReplicationHack;
            set => ThreadedReplicationServer.UseReplicationHack = value;
        }

#pragma warning disable 649
        [ReflectedMethodInfo(typeof(BitStream), "Clear")]
        private static readonly MethodInfo _bitStreamClear;

        [ReflectedFieldInfo(typeof(BitStream), "m_buffer")]
        private static readonly FieldInfo _bitStreamBuffer;

        [ReflectedFieldInfo(typeof(BitStream), "m_bitLength")]
        private static readonly FieldInfo _bitStreamBitLength;
#pragma warning restore 649

        private static IEnumerable<MsilInstruction> TranspileClearBitStream(IEnumerable<MsilInstruction> ins, Func<Type, MsilLocal> __localCreator)
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
            yield return new MsilInstruction(OpCodes.Ldind_I8);             // original
            yield return new MsilInstruction(OpCodes.Ldc_I4_M1);            
            yield return new MsilInstruction(OpCodes.Conv_I8);              
            yield return bitsToSkip.AsValueLoad();                          // original, -1, bitsToSkip
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
    }
}