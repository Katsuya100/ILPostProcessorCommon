using Mono.Cecil;
using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class MemberReferenceComparer : IEqualityComparer<MemberReference>
    {
        public static readonly MemberReferenceComparer Default = new MemberReferenceComparer();
        public bool Equals(MemberReference x, MemberReference y)
        {
            if (x is TypeReference tx && y is TypeReference ty)
            {
                return tx.Is(ty);
            }

            if (x is MethodReference mx && y is MethodReference my)
            {
                return mx.Is(my);
            }

            if (x is FieldReference fx && y is FieldReference fy)
            {
                return fx.Is(fy);
            }

            if (x is PropertyReference px && y is PropertyReference py)
            {
                return px.Is(py);
            }

            if (x is EventReference ex && y is EventReference ey)
            {
                return ex.Is(ey);
            }

            if (x == y)
            {
                return true;
            }

            return false;
        }

        public int GetHashCode(MemberReference obj)
        {
            if (obj is TypeReference t)
            {
                return t.GetHashCode_();
            }

            if (obj is MethodReference m)
            {
                return m.GetHashCode_();
            }

            if (obj is FieldReference f)
            {
                return f.GetHashCode_();
            }

            if (obj is PropertyReference p)
            {
                return p.GetHashCode_();
            }

            if (obj is EventReference e)
            {
                return e.GetHashCode_();
            }

            return obj.GetHashCode();
        }
    }
}
