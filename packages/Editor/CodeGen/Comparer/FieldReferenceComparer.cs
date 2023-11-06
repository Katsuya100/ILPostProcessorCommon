using Mono.Cecil;
using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class FieldReferenceComparer : IEqualityComparer<FieldReference>
    {
        public static readonly FieldReferenceComparer Default = new FieldReferenceComparer();
        public bool Equals(FieldReference x, FieldReference y)
        {
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.Name != y.Name)
            {
                return false;
            }

            var result = x.DeclaringType.Is(y.DeclaringType);
            return result;
        }

        public int GetHashCode(FieldReference obj)
        {
            if (obj == null)
            {
                return 0;
            }

            int hash = obj.Name.GetHashCode();
            hash ^= obj.DeclaringType.GetHashCode_();
            return hash;
        }
    }
}
