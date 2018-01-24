using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Network;
using VRage.Replication;

namespace FastReplicate
{
    public class BandwidthCounter
    {
        private readonly MyBandwidthLimits _backing;

        private readonly Dictionary<StateGroupEnum, Ref<int>> _counters = new Dictionary<StateGroupEnum, Ref<int>>();

        public BandwidthCounter(MyBandwidthLimits limits)
        {
            _backing = limits;
        }

        /// <summary>
        /// Gets current limit for group (how many bits can be written per frame).
        /// Return zero when there's no limit.
        /// </summary>
        public int GetLimit(StateGroupEnum group)
        {
            return _backing?.GetLimit(group) ?? 0;
        }

        public bool Add(StateGroupEnum group, int bitCount)
        {
            int limit = GetLimit(group);
            if (limit == 0)
                return true;
            if (!this._counters.TryGetValue(group, out var @ref))
            {
                @ref = new Ref<int>();
                _counters[group] = @ref;
            }

            if (@ref.Value <= limit)
                @ref.Value += bitCount;
            return @ref.Value <= limit;
        }

        public void Clear()
        {
            foreach (KeyValuePair<StateGroupEnum, Ref<int>> bandwidthCounter in this._counters)
                bandwidthCounter.Value.Value = 0;
        }
    }
}