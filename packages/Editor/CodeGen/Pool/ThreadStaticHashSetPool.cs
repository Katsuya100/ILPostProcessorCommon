using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class ThreadStaticHashSetPool<T> : ThreadStaticCollectionPool<HashSet<T>, T, ThreadStaticHashSetPool<T>>
    {
        public static Handle Get(out HashSet<T> result, IEnumerable<T> init)
        {
            var ret = Get(out result);
            result.UnionWith(init);
            return ret;
        }

        public static HashSet<T> Get(IEnumerable<T> init)
        {
            var result = Get();
            result.UnionWith(init);
            return result;
        }
    }

    public static class ThreadStaticHashSetPool
    {
        public static ThreadStaticHashSetPool<T>.Handle Get<T>(out HashSet<T> result)
        {
            return ThreadStaticHashSetPool<T>.Get(out result);
        }

        public static HashSet<T> Get<T>()
        {
            return ThreadStaticHashSetPool<T>.Get();
        }

        public static ThreadStaticHashSetPool<T>.Handle Get<T>(out HashSet<T> result, IEnumerable<T> init)
        {
            return ThreadStaticHashSetPool<T>.Get(out result, init);
        }

        public static HashSet<T> Get<T>(IEnumerable<T> init)
        {
            return ThreadStaticHashSetPool<T>.Get(init);
        }
    }
}