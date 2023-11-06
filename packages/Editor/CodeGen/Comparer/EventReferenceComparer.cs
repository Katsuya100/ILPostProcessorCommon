using Mono.Cecil;
using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class EventReferenceComparer : IEqualityComparer<EventReference>
    {
        public static readonly EventReferenceComparer Default = new EventReferenceComparer();
        public bool Equals(EventReference x, EventReference y)
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

        public int GetHashCode(EventReference obj)
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
