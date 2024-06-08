using System;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public static class ThreadStaticArrayPool<T>
    {
        [ThreadStatic]
        private static Stack<T[]>[] _pools;

        public ref struct Handle
        {
            private T[] _array;
            public Handle(T[] array)
            {
                _array = array;
            }

            public void Dispose()
            {
                if (_array == null)
                {
                    return;
                }
                Return(_array);
                _array = default;
            }
        }

        public static Handle Get(out T[] result, int length)
        {
            result = Get(length);
            return new Handle(result);
        }

        public static Handle Get(out T[] result, int length, IEnumerable<T> init)
        {
            result = Get(length);
            int i = 0;
            foreach (var e in init)
            {
                result[i] = e;
                ++i;
            }
            return new Handle(result);
        }

        public static Handle Get(out T[] result, IEnumerable<T> init)
        {
            return Get(out result, init.Count(), init);
        }

        public static T[] Get(int length, IEnumerable<T> init)
        {
            var result = Get(length);
            int i = 0;
            foreach (var e in init)
            {
                result[i] = e;
                ++i;
            }
            return result;
        }

        public static T[] Get(IEnumerable<T> init)
        {
            return Get(init.Count(), init);
        }

        public static T[] Get(int length)
        {
            if (_pools == null)
            {
                _pools = new Stack<T[]>[16];
            }

            if (length >= _pools.Length)
            {
                Array.Resize(ref _pools, length * 2);
            }

            var pool = _pools[length];
            if (pool == null)
            {
                pool = new Stack<T[]>();
                _pools[length] = pool;
            }

            if (pool.Count <= 0)
            {
                return new T[length];
            }

            return pool.Pop();
        }

        public static void Return(T[] array)
        {
            if (array == null)
            {
                return;
            }

            var length = array.Length;
            if (_pools == null)
            {
                _pools = new Stack<T[]>[16];
            }

            if (length >= _pools.Length)
            {
                Array.Resize(ref _pools, length * 2);
            }

            var pool = _pools[length];
            if (pool == null)
            {
                pool = new Stack<T[]>();
                _pools[length] = pool;
            }

            Array.Clear(array, 0, array.Length);
            pool.Push(array);
        }
    }

    public static class ThreadStaticArrayPool
    {
        public static ThreadStaticArrayPool<T>.Handle Get<T>(out T[] result, int length)
        {
            return ThreadStaticArrayPool<T>.Get(out result, length);
        }

        public static T[] Get<T>(int length)
        {
            return ThreadStaticArrayPool<T>.Get(length);
        }

        public static ThreadStaticArrayPool<T>.Handle Get<T>(out T[] result, int length, IEnumerable<T> init)
        {
            return ThreadStaticArrayPool<T>.Get(out result, length, init);
        }

        public static T[] Get<T>(int length, IEnumerable<T> init)
        {
            return ThreadStaticArrayPool<T>.Get(length, init);
        }

        public static ThreadStaticArrayPool<T>.Handle Get<T>(out T[] result, IEnumerable<T> init)
        {
            return ThreadStaticArrayPool<T>.Get(out result, init);
        }

        public static T[] Get<T>(IEnumerable<T> init)
        {
            return ThreadStaticArrayPool<T>.Get(init);
        }
    }
}