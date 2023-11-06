using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class LiteralComparer : IEqualityComparer<object>
    {
        public const int Prime = 31;
        public static readonly LiteralComparer Default = new LiteralComparer();

        public new bool Equals(object x, object y)
        {
            if (x.GetType() != y.GetType())
            {
                return false;
            }

            if (x is IReadOnlyArray xe &&
                y is IReadOnlyArray ye)
            {
                return xe.Cast<object>().SequenceEqual(ye.Cast<object>(), Default);
            }

            return x.Equals(y);
        }

        bool IEqualityComparer<object>.Equals(object x, object y)
        {
            return Equals(x, y);
        }

        public int GetHashCode(object obj)
        {
            if (!(obj is IReadOnlyArray oe))
            {
                return obj.GetHashCode();
            }

            int hash = 1;
            foreach (object o in oe)
            {
                hash = hash * Prime + o.GetHashCode();
            }

            return hash;
        }
    }
}
