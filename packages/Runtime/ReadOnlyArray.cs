using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Katuusagi.ILPostProcessorCommon
{
    public sealed class ReadOnlyArray<T> : IReadOnlyArray, IReadOnlyList<T>
    {
        private T[] _array;

        private ReadOnlyArray()
        {
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array[index];
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return ((IEnumerable<T>)_array).GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator GetEnumerator()
        {
            return _array.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] dst)
        {
            for (int i = 0; i < _array.Length && i < dst.Length; ++i)
            {
                dst[i] = _array[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Span<T> dst)
        {
            for (int i = 0; i < _array.Length && i < dst.Length; ++i)
            {
                dst[i] = _array[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsSpan()
        {
            return _array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(ReadOnlyArray<T> b)
        {
            return b._array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyArray<T>(T[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            var instance = new ReadOnlyArray<T>();
            instance._array = array;
            return instance;
        }
    }
}
