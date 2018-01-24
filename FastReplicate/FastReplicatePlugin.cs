using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using VRage.Network;
using VRage.Replication;

namespace FastReplicate
{
    /// <summary>
    /// Plugin FastReplicate
    /// </summary>
    public class FastReplicatePlugin : TorchPluginBase
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
            
            GeneratedMethods.Init();
            mgr.Commit();
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