using System.Runtime.CompilerServices;

namespace Katuusagi.ILPostProcessorCommon.Utils
{
    public static class ReadOnlyArrayUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<T> ConvertArrayToReadOnlyArray<T>(T[] array)
        {
            return array;
        }
    }
}
