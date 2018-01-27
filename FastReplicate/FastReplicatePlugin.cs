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
using Torch.Collections;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage.Library.Collections;
using VRage.Network;
using VRage.Replication;
using VRageMath;

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
            Patch.Apply(ctx);
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

        public float TargetPacketFill
        {
            get => ThreadedReplicationServer.TargetPacketFill;
            set => ThreadedReplicationServer.TargetPacketFill = MathHelper.Clamp(value, 0.25f, 1);
        }

        public MtObservableSortedDictionary<ulong, ClientStatsViewModel> ClientStats =>ThreadedReplicationServer.ClientStats;
    }
}