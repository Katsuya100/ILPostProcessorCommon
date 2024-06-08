using System;
using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class ThreadStaticCollectionPool<TCollection, TElement, TVariant>
        where TCollection : ICollection<TElement>, new()
    {
        [ThreadStatic]
        private static Stack<TCollection> _pool;

        protected static Func<TCollection> _createInstance = () => new TCollection();

        public ref struct Handle
        {
            private TCollection _collection;
            public Handle(TCollection collection)
            {
                _collection = collection;
            }

            public void Dispose()
            {
                if (_collection == null)
                {
                    return;
                }
                Return(_collection);
                _collection = default;
            }
        }

        public static Handle Get(out TCollection result)
        {
            result = Get();
            return new Handle(result);
        }

        public static TCollection Get()
        {
            if (_pool == null)
            {
                _pool = new Stack<TCollection>();
            }

            if (_pool.Count <= 0)
            {
                return _createInstance();
            }

            return _pool.Pop();
        }

        public static void Return(TCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            if (_pool == null)
            {
                _pool = new Stack<TCollection>();
            }

            collection.Clear();
            _pool.Push(collection);
        }
    }
}