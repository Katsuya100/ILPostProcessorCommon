using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class ThreadStaticListPool<T> : ThreadStaticCollectionPool<List<T>, T, ThreadStaticListPool<T>>
    {
        public static Handle Get(out List<T> result, IEnumerable<T> init)
        {
            var ret = Get(out result);
            result.AddRange(init);
            return ret;
        }

        public static List<T> Get(IEnumerable<T> init)
        {
            var result = Get();
            result.AddRange(init);
            return result;
        }
    }

    public static class ThreadStaticListPool
    {
        public static ThreadStaticListPool<T>.Handle Get<T>(out List<T> result)
        {
            return ThreadStaticListPool<T>.Get(out result);
        }

        public static List<T> Get<T>()
        {
            return ThreadStaticListPool<T>.Get();
        }

        public static ThreadStaticListPool<T>.Handle Get<T>(out List<T> result, IEnumerable<T> init)
        {
            return ThreadStaticListPool<T>.Get(out result, init);
        }

        public static List<T> Get<T>(IEnumerable<T> init)
        {
            return ThreadStaticListPool<T>.Get(init);
        }
    }
}