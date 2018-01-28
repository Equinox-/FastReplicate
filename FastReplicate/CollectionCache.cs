using System;
using System.Collections.Generic;
using System.Threading;
using VRageMath;

namespace FastReplicate
{
    public static class CollectionCache<TC, TV> where TC : ICollection<TV>, new()
    {
        private static readonly ThreadLocal<Stack<TC>> _replicableListCache =
            new ThreadLocal<Stack<TC>>(() => new Stack<TC>());


        public static CollectionToken Borrow()
        {
            var cache = _replicableListCache.Value;
            return new CollectionToken(cache.Count > 0 ? cache.Pop() : new TC());
        }

        public struct CollectionToken : IDisposable
        {
            public readonly TC Value;

            public CollectionToken(TC lst)
            {
                lst.Clear();
                Value = lst;
            }

            public void Set(ICollection<TV> rep)
            {
                var lst = Value as List<TV>;
                if (lst != null)
                    lst.Set(rep);
                else
                    SetEnumerated(rep);
            }

            public void SetEnumerated(IEnumerable<TV> rep)
            {
                Value.Clear();
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

    public static class ListExtensions
    {
        public static void Set<T>(this List<T> list, ICollection<T> collection)
        {
            list.Clear();
            if (collection.Count == 0)
                return;
            int sizeFinal = MathHelper.GetNearestBiggerPowerOfTwo(collection.Count);
            if (list.Capacity < sizeFinal || list.Capacity * 8 > sizeFinal)
                list.Capacity = sizeFinal;
            var arrrr = list.GetInternalArray();
            var count = collection.Count;
            collection.CopyTo(arrrr, 0);
            list.SetSize(count);
        }
    }
}