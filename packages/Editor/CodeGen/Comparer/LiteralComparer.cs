using System;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class LiteralComparer : IEqualityComparer<(Type t, object o)>
    {
        public const int Prime = 31;
        public static readonly LiteralComparer Default = new LiteralComparer();

        public bool Equals((Type t, object o) x, (Type t, object o) y)
        {
            if (x.t != y.t ||
                x.o.GetType() != y.o.GetType())
            {
                return false;
            }

            if (x.o is IReadOnlyArray xe &&
                y.o is IReadOnlyArray ye)
            {
                var xs = xe.Cast<object>().Select(v => (v.GetType(), v));
                var ys = ye.Cast<object>().Select(v => (v.GetType(), v));

                return xs.SequenceEqual(ys, Default);
            }

            return x.o.Equals(y.o);
        }

        bool IEqualityComparer<(Type t, object o)>.Equals((Type t, object o) x, (Type t, object o) y)
        {
            return Equals(x, y);
        }

        public int GetHashCode((Type t, object o) obj)
        {
            int hash = obj.t.GetType().GetHashCode();

            if (!(obj.o is IReadOnlyArray oe))
            {
                return hash ^ obj.o.GetHashCode();
            }

            foreach (object o in oe)
            {
                hash = hash * Prime + o.GetHashCode();
            }

            return hash;
        }
    }
}
