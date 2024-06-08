using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class MethodReferenceComparer : IEqualityComparer<MethodReference>
    {
        public static readonly MethodReferenceComparer Default = new MethodReferenceComparer();
        public bool Equals(MethodReference x, MethodReference y)
        {
            x = x.ResolveVirtualElementMethod();
            y = y.ResolveVirtualElementMethod();
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.GetType() == y.GetType())
            {
                if (x is GenericInstanceMethod gx &&
                    y is GenericInstanceMethod gy)
                {
                    if (!gx.GenericArguments.SequenceEqual(gy.GenericArguments, TypeReferenceComparer.Default))
                    {
                        return false;
                    }
                }
            }

            if (x.Name != y.Name)
            {
                return false;
            }

            var xd = x.DeclaringType;
            var yd = y.DeclaringType;
            if (!xd.Is(yd))
            {
                return false;
            }

            var isGenDefX = x.IsGenericDefinition();
            var isGenDefY = y.IsGenericDefinition();
            if (isGenDefX != isGenDefY)
            {
                return false;
            }
            if (isGenDefX)
            {
                var xgr = x.ReturnType;
                var ygr = y.ReturnType;
                if (!TypeReferenceComparer.SkipGenericMethodOwner.Equals(xgr, ygr))
                {
                    return false;
                }

                if (!x.Parameters.Select(v => v.ParameterType).SequenceEqual(y.Parameters.Select(v => v.ParameterType), TypeReferenceComparer.SkipGenericMethodOwner))
                {
                    return false;
                }

                return x.GenericParameters.Count == y.GenericParameters.Count;
            }

            var xr = x.ReturnType;
            var yr = y.ReturnType;
            if (!xr.Is(yr))
            {
                return false;
            }

            if (!x.Parameters.Select(v => v.ParameterType).SequenceEqual(y.Parameters.Select(v => v.ParameterType), TypeReferenceComparer.Default))
            {
                return false;
            }

            return true;
        }

        public int GetHashCode(MethodReference obj)
        {
            obj = obj.ResolveVirtualElementMethod();
            if (obj == null)
            {
                return 0;
            }

            int hash = 0;
            if (obj is GenericInstanceMethod go)
            {
                foreach (var genArg in go.GenericArguments)
                {
                    hash ^= genArg.GetHashCode_();
                }
            }

            hash ^= obj.Name.GetHashCode();
            hash ^= obj.DeclaringType.GetHashCode_();

            if (obj.IsGenericDefinition())
            {
                foreach (var p in obj.Parameters)
                {
                    hash ^= TypeReferenceComparer.SkipGenericMethodOwner.GetHashCode(p.ParameterType);
                }

                hash ^= obj.GenericParameters.Count;
                return hash;
            }

            foreach (var p in obj.Parameters)
            {
                hash ^= p.ParameterType.GetHashCode_();
            }

            return hash;
        }
    }
}
