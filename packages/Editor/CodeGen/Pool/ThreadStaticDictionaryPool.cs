using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class ThreadStaticDictionaryPool<TKey, TValue> : ThreadStaticCollectionPool<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>, ThreadStaticDictionaryPool<TKey, TValue>>
    {
        public static Handle Get(out Dictionary<TKey, TValue> result, IEnumerable<KeyValuePair<TKey, TValue>> init)
        {
            var ret = Get(out result);
            foreach (var e in init)
            {
                result.Add(e.Key, e.Value);
            }
            return ret;
        }

        public static Dictionary<TKey, TValue> Get(IEnumerable<KeyValuePair<TKey, TValue>> init)
        {
            var result = Get();
            foreach (var e in init)
            {
                result.Add(e.Key, e.Value);
            }
            return result;
        }
    }

    public static class ThreadStaticDictionaryPool
    {
        public static ThreadStaticDictionaryPool<TKey, TValue>.Handle Get<TKey, TValue>(out Dictionary<TKey, TValue> result)
        {
            return ThreadStaticDictionaryPool<TKey, TValue>.Get(out result);
        }

        public static Dictionary<TKey, TValue> Get<TKey, TValue>()
        {
            return ThreadStaticDictionaryPool<TKey, TValue>.Get();
        }

        public static ThreadStaticDictionaryPool<TKey, TValue>.Handle Get<TKey, TValue>(out Dictionary<TKey, TValue> result, IEnumerable<KeyValuePair<TKey, TValue>> init)
        {
            return ThreadStaticDictionaryPool<TKey, TValue>.Get(out result, init);
        }

        public static Dictionary<TKey, TValue> Get<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> init)
        {
            return ThreadStaticDictionaryPool<TKey, TValue>.Get(init);
        }
    }
}