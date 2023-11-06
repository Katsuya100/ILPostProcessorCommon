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

            if (x.Name != y.Name)
            {
                return false;
            }

            if (x.IsGenericDefinition())
            {
                if (!y.IsGenericDefinition())
                {
                    return false;
                }

                if (!x.Parameters.Select(v => v.ParameterType.FullName).SequenceEqual(y.Parameters.Select(v => v.ParameterType.FullName)))
                {
                    return false;
                }

                var xd = x.DeclaringType?.GetElementType();
                var yd = y.DeclaringType?.GetElementType();
                if (!xd.Is(yd))
                {
                    return false;
                }

                return x.GenericParameters.Count == y.GenericParameters.Count;
            }

            if (!x.Parameters.Select(v => v.ParameterType).SequenceEqual(y.Parameters.Select(v => v.ParameterType), TypeReferenceComparer.Default))
            {
                return false;
            }

            if (!x.DeclaringType.Is(y.DeclaringType))
            {
                return false;
            }

            if (x is GenericInstanceMethod gx)
            {
                if (!(y is GenericInstanceMethod gy))
                {
                    return false;
                }

                return gx.GenericArguments.SequenceEqual(gy.GenericArguments, TypeReferenceComparer.Default);
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

            int hash = obj.Name.GetHashCode();
            if (obj.IsGenericDefinition())
            {
                foreach (var p in obj.Parameters)
                {
                    hash ^= p.ParameterType.FullName.GetHashCode();
                }

                var td = obj.DeclaringType?.GetElementType();
                hash ^= td.GetHashCode_();
                hash ^= obj.GenericParameters.Count;
                return hash;
            }

            foreach (var p in obj.Parameters)
            {
                hash ^= p.ParameterType.GetHashCode_();
            }

            hash ^= obj.DeclaringType.GetHashCode_();
            if (obj is GenericInstanceMethod go)
            {
                foreach (var genArg in go.GenericArguments)
                {
                    hash ^= genArg.GetHashCode_();
                }
                return hash;
            }

            return hash;
        }
    }
}
