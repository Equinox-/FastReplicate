using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRage.Network;
using VRageMath;

namespace FastReplicate
{
    public static class ListCache<T>
    {
        private static readonly ThreadLocal<Stack<List<T>>> _replicableListCache =
            new ThreadLocal<Stack<List<T>>>(() => new Stack<List<T>>());


        public static ReplicableListToken BorrowList()
        {
            var cache = _replicableListCache.Value;
            return new ReplicableListToken(cache.Count > 0 ? cache.Pop() : new List<T>());
        }

        public struct ReplicableListToken : IDisposable
        {
            public readonly List<T> Value;

            public ReplicableListToken(List<T> lst)
            {
                lst.Clear();
                Value = lst;
            }

            public void Set(ICollection<T> rep)
            {
                int sizeFinal = MathHelper.GetNearestBiggerPowerOfTwo(rep.Count);
                if (Value.Capacity < sizeFinal || Value.Capacity * 8 > sizeFinal)
                    Value.Capacity = sizeFinal;
                rep.CopyTo(Value.GetInternalArray(), 0);
                Value.SetSize(rep.Count);
            }

            public void SetEnumerated(IEnumerable<T> rep)
            {
                foreach (var k in rep)
                    Value.Add(k);
            }

            public void Dispose()
            {
                Value.Clear();
                var cache = _replicableListCache.Value;
                if (cache.Count < 32)
                    cache.Push(Value);
            }
        }
    }
}
