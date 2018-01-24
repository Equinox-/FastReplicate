using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Torch.Managers.PatchManager.Transpile;
using Torch.Utils;
using VRage.Collections;
using VRage.Library.Utils;
using VRage.Network;

namespace FastReplicate
{
    public static class GeneratedMethods
    {
        private static readonly LogLevel _level = LogLevel.Trace;

        public static Func<object, MyClientInfo> AllocateClientInfo;

        private static Func<object, MyClientInfo> GenerateAllocateClientInfo()
        {
            var clientDataType = Type.GetType(ClientData.InternalTypeName);
            if (clientDataType == null)
                throw new InvalidOperationException("Couldn't find " +
                                                    ClientData.InternalTypeName);
            var ctor = typeof(MyClientInfo).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] {clientDataType}, null);
            if (ctor == null)
                throw new InvalidOperationException("Couldn't find MyClientInfo(ClientData)");

            var meth = new DynamicMethod(nameof(AllocateClientInfo), typeof(MyClientInfo), new[] {typeof(object)},
                true);
            var gen = new LoggingIlGenerator(meth.GetILGenerator(), _level);
            gen.EmitComment("// Start" + nameof(AllocateClientInfo));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, clientDataType);
            gen.Emit(OpCodes.Newobj, ctor);
            gen.Emit(OpCodes.Ret);
            gen.EmitComment("// End" + nameof(AllocateClientInfo));
            Check(meth);
            return meth.CreateDelegate<Func<object, MyClientInfo>>();
        }

        private static void EmitCopyAs(LoggingIlGenerator gen, LocalBuilder src, LocalBuilder dst)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            Debug.Assert(src.LocalType != null, "src.LocalType != null");
            Debug.Assert(dst.LocalType != null, "dst.LocalType != null");
            var fields = src.LocalType.GetFields(flags);
            gen.EmitComment($"Copy {src.LocalType} to {dst.LocalType}");
            gen.Emit(OpCodes.Ldloca, dst);
            gen.Emit(OpCodes.Initobj, dst.LocalType);
            foreach (var sf in fields)
            {
                gen.Emit(OpCodes.Ldloca, dst);
                gen.Emit(OpCodes.Ldloca, src);
                var df = dst.LocalType?.GetField(sf.Name, flags);
                Debug.Assert(df != null, $"{dst.LocalType}.{sf.Name} != null");
                gen.Emit(OpCodes.Ldfld, sf);
                gen.Emit(OpCodes.Stfld, df);
            }

            gen.EmitComment("EndCopy");
        }

        public static Func<object, IEnumerator<IslandData>> ClientDataIslandsEnumerator;

        public static Func<object, IMyReplicable, HashSet<IMyReplicable>, MyTimeSpan, IslandData>
            CreateNewCachedIsland;

        private static Action<object> GenerateCachingIslandDataApplyRemovals()
        {
            var type = Type.GetType(_clientDataIsland);
            if (type == null)
                throw new InvalidOperationException("Couldn't find " + _clientDataIsland);

            var cacheType = typeof(CachingHashSet<>).MakeGenericType(type);
            var backing = cacheType.GetMethod("ApplyRemovals", BindingFlags.Instance | BindingFlags.Public);
            if (backing == null)
                throw new InvalidOperationException("Couldn't find ApplyRemovals");
            var meth = new DynamicMethod(nameof(ClientDataIslandsApplyRemovals), typeof(void),
                new[] {typeof(object)}, true);
            var gen = new LoggingIlGenerator(meth.GetILGenerator(), _level);
            gen.EmitComment("// Start " + meth.Name);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, cacheType);
            gen.Emit(OpCodes.Callvirt, backing);
            gen.Emit(OpCodes.Ret);
            gen.EmitComment("// End " + meth.Name);
            Check(meth);
            return meth.CreateDelegate<Action<object>>();
        }

        public static void Init()
        {
            AllocateClientInfo = GenerateAllocateClientInfo();

            var islandProxy = GenerateClientDataIslandsEnumerator();
            ClientDataIslandsEnumerator = islandProxy.Create;

            CreateNewCachedIsland = GenerateCreateNewCachedIsland();
            RemoveCachedIsland = GenerateRemoveCachedIsland();

            ClientDataIslandsApplyRemovals = GenerateCachingIslandDataApplyRemovals();
            ClientDataReplicableToIslandTryGetValue = GenerateClientDataReplicableToIslandTryGetValue();
        }

        private static TryGetValueTemplate<IMyStreamableReplicable, IslandData>
            GenerateClientDataReplicableToIslandTryGetValue()
        {
            var type = Type.GetType(_clientDataIsland);
            if (type == null)
                throw new InvalidOperationException("Couldn't find " + _clientDataIsland);

            var cacheType = typeof(Dictionary<,>).MakeGenericType(typeof(IMyStreamableReplicable), type);
            var backing = cacheType.GetMethod("TryGetValue", BindingFlags.Instance | BindingFlags.Public);
            if (backing == null)
                throw new InvalidOperationException("Couldn't find TryGetValue");
            var meth = new DynamicMethod(nameof(ClientDataReplicableToIslandTryGetValue), typeof(bool),
                new[] {typeof(object), typeof(IMyStreamableReplicable), typeof(IslandData).MakeByRefType()}, true);
            var gen = new LoggingIlGenerator(meth.GetILGenerator(), _level);
            gen.EmitComment("// Start " + meth.Name);
            var islandInternal = gen.DeclareLocal(type);
            var islandExternal = gen.DeclareLocal(typeof(IslandData));
            var tmpResult = gen.DeclareLocal(typeof(bool));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, cacheType);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldloca, islandInternal);
            gen.Emit(OpCodes.Callvirt, backing);
            gen.Emit(OpCodes.Stloc, tmpResult);

            EmitCopyAs(gen, islandInternal, islandExternal);

            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldloc, islandExternal);
            gen.Emit(OpCodes.Stobj, typeof(IslandData));

            gen.Emit(OpCodes.Ldloc, tmpResult);
            gen.Emit(OpCodes.Ret);
            gen.EmitComment("// End " + meth.Name);

            Check(meth);
            return (TryGetValueTemplate<IMyStreamableReplicable, IslandData>) meth.CreateDelegate(
                typeof(TryGetValueTemplate<IMyStreamableReplicable, IslandData>));
        }

        public static Action<object> ClientDataIslandsApplyRemovals;

        public static TryGetValueTemplate<IMyStreamableReplicable, IslandData>
            ClientDataReplicableToIslandTryGetValue;

        public delegate bool TryGetValueTemplate<in TK, TV>(object obj, TK key, out TV value);

        private static Func<object, IMyReplicable, HashSet<IMyReplicable>, MyTimeSpan, IslandData>
            GenerateCreateNewCachedIsland()
        {
            var clientDataType = Type.GetType(ClientData.InternalTypeName);
            if (clientDataType == null)
                throw new InvalidOperationException("Couldn't fine " +
                                                    ClientData.InternalTypeName);

            var type = Type.GetType(_clientDataIsland);
            if (type == null)
                throw new InvalidOperationException("Couldn't find " + _clientDataIsland);

            var backing = clientDataType.GetMethod("CreateNewCachedIsland");
            if (backing == null)
                throw new InvalidOperationException("Couldn't find CreateNewCachedIsland");

            var meth = new DynamicMethod(nameof(CreateNewCachedIsland), typeof(IslandData),
                new[] {typeof(object), typeof(IMyReplicable), typeof(HashSet<IMyReplicable>), typeof(MyTimeSpan)},
                true);

            var gen = new LoggingIlGenerator(meth.GetILGenerator(), _level);
            gen.EmitComment("// Start " + meth.Name);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, clientDataType);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Callvirt, backing);

            var islandInternal = gen.DeclareLocal(type);
            var islandExternal = gen.DeclareLocal(typeof(IslandData));
            gen.Emit(OpCodes.Stloc, islandInternal);
            EmitCopyAs(gen, islandInternal, islandExternal);
            gen.Emit(OpCodes.Ldloc, islandExternal);
            gen.Emit(OpCodes.Ret);
            gen.EmitComment("// End " + meth.Name);

            Check(meth);
            return meth.CreateDelegate<Func<object, IMyReplicable, HashSet<IMyReplicable>, MyTimeSpan, IslandData>>();
        }

        public static Action<object, IslandData> RemoveCachedIsland;

        private static Action<object, IslandData> GenerateRemoveCachedIsland()
        {
            var clientDataType = Type.GetType(ClientData.InternalTypeName);
            if (clientDataType == null)
                throw new InvalidOperationException("Couldn't fine " +
                                                    ClientData.InternalTypeName);

            var type = Type.GetType(_clientDataIsland);
            if (type == null)
                throw new InvalidOperationException("Couldn't find " + _clientDataIsland);

            var backing = clientDataType.GetMethod("RemoveCachedIsland");
            if (backing == null)
                throw new InvalidOperationException("Couldn't find RemoveCachedIsland");
            var meth = new DynamicMethod(nameof(RemoveCachedIsland), typeof(void),
                new[] {typeof(object), typeof(IslandData)}, true);
            var gen = new LoggingIlGenerator(meth.GetILGenerator(), _level);
            gen.EmitComment("// Start " + meth.Name);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, clientDataType);
            var islandExternal = gen.DeclareLocal(typeof(IslandData));
            var islandInternal = gen.DeclareLocal(type);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stloc, islandExternal);
            EmitCopyAs(gen, islandExternal, islandInternal);
            gen.Emit(OpCodes.Ldloc, islandInternal);
            gen.Emit(OpCodes.Callvirt, backing);
            gen.Emit(OpCodes.Ret);
            gen.EmitComment("// End " + meth.Name);

            Check(meth);
            return meth.CreateDelegate<Action<object, IslandData>>();
        }

        private static readonly string _clientDataIsland =
            typeof(MyReplicationServer).FullName + "+ClientData+IslandData, " +
            typeof(MyReplicationServer).Assembly.GetName().Name;

        private static DynamicProxyEnumerable<IslandData> GenerateClientDataIslandsEnumerator()
        {
            var type = Type.GetType(_clientDataIsland);
            if (type == null)
                throw new InvalidOperationException("Couldn't find " + _clientDataIsland);

            var meth = new DynamicMethod(nameof(ClientDataIslandsEnumerator) + "_Mapper", typeof(IslandData),
                new[] {typeof(IEnumerator)}, true);

            var genericEnumerator = typeof(IEnumerator<>).MakeGenericType(type);
            var currentProp = genericEnumerator.GetProperty("Current");
            if (currentProp == null)
                throw new InvalidOperationException("Couldn't find Current property on " + genericEnumerator.FullName);

            var gen = new LoggingIlGenerator(meth.GetILGenerator(), _level);
            var islandInternal = gen.DeclareLocal(type);
            var islandExternal = gen.DeclareLocal(typeof(IslandData));

            gen.EmitComment("// Start " + meth.Name);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, genericEnumerator);
            gen.Emit(OpCodes.Callvirt, currentProp.GetMethod);
            gen.Emit(OpCodes.Stloc, islandInternal);
            EmitCopyAs(gen, islandInternal, islandExternal);
            gen.Emit(OpCodes.Ldloc, islandExternal);
            gen.Emit(OpCodes.Ret);
            gen.EmitComment("// End " + meth.Name);

            Check(meth);
            return new DynamicProxyEnumerable<IslandData>((o) => ((IEnumerable) o).GetEnumerator(),
                meth.CreateDelegate<Func<IEnumerator, IslandData>>());
        }

#pragma warning disable 649
        [ReflectedStaticMethod(Type = typeof(RuntimeHelpers), Name = "_CompileMethod",
            OverrideTypeNames = new[] {"System.IRuntimeMethodInfo"})]
        private static Action<object> _compileDynamicMethod;

        [ReflectedMethod(Name = "GetMethodInfo")]
        private static Func<RuntimeMethodHandle, object> _getMethodInfo;

        [ReflectedMethod(Name = "GetMethodDescriptor")]
        private static Func<DynamicMethod, RuntimeMethodHandle> _getMethodHandle;
#pragma warning restore 649

        private static void Check(DynamicMethod meth)
        {
            // Force it to compile
            RuntimeMethodHandle handle = _getMethodHandle.Invoke(meth);
            object runtimeMethodInfo = _getMethodInfo.Invoke(handle);
            _compileDynamicMethod.Invoke(runtimeMethodInfo);
        }

        public struct IslandData
        {
            public HashSet<IMyStreamableReplicable> Replicables;
            public byte Index;
        }

        private class DynamicProxyEnumerable<T>
        {
            private readonly Func<IEnumerator, T> _mapper;
            private readonly Func<object, IEnumerator> _allocator;

            public DynamicProxyEnumerable(Func<object, IEnumerator> allocate, Func<IEnumerator, T> map)
            {
                _allocator = allocate;
                _mapper = map;
            }

            public IEnumerator<T> Create(object o)
            {
                return new Enumerator(_allocator(o), _mapper);
            }

            private class Enumerator : IEnumerator<T>
            {
                private readonly IEnumerator _e;
                private readonly Func<IEnumerator, T> _current;

                internal Enumerator(IEnumerator e, Func<IEnumerator, T> mapper)
                {
                    _e = e;
                    _current = mapper;
                }

                public void Dispose()
                {
                    (_e as IDisposable)?.Dispose();
                }

                public bool MoveNext()
                {
                    return _e.MoveNext();
                }

                public void Reset()
                {
                    _e.Reset();
                }

                public T Current => _current(_e);

                object IEnumerator.Current
                {
                    get { return Current; }
                }
            }
        }
    }
}