using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public static readonly TypeReferenceComparer Default = new TypeReferenceComparer();
        public bool Equals(TypeReference x, TypeReference y)
        {
            x = x.ResolveVirtualElementType();
            y = y.ResolveVirtualElementType();
            if (x == y)
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x is GenericParameter gpx)
            {
                if (!(y is GenericParameter gpy))
                {
                    return false;
                }

                if (gpx.Owner is TypeReference ox)
                {
                    if (!(gpy.Owner is TypeReference oy))
                    {
                        return false;
                    }

                    if (!ox.GetElementType().Is(oy.GetElementType()))
                    {
                        return false;
                    }
                }
                else if (gpx.Owner is MethodReference omx)
                {
                    if (!(gpy.Owner is MethodReference omy))
                    {
                        return false;
                    }

                    if (!omx.GetElementMethod().Is(omy.GetElementMethod()))
                    {
                        return false;
                    }
                }
                else if (gpx.Owner != gpy.Owner)
                {
                    return false;
                }

                return gpx.Position == gpy.Position;
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

                var xd = x.DeclaringType?.GetElementType();
                var yd = y.DeclaringType?.GetElementType();
                if (!xd.Is(yd))
                {
                    return false;
                }

                return x.GenericParameters.Count == y.GenericParameters.Count;
            }

            if (!Equals(x.DeclaringType, y.DeclaringType))
            {
                return false;
            }

            if (x is GenericInstanceType gx)
            {
                if (!(y is GenericInstanceType gy))
                {
                    return false;
                }

                return gx.GenericArguments.SequenceEqual(gy.GenericArguments, Default);
            }

            if (x is ArrayType ax)
            {
                if (!(y is ArrayType ay))
                {
                    return false;
                }

                if (ax.Rank != ay.Rank)
                {
                    return false;
                }
                return Equals(ax.ElementType, ay.ElementType);
            }

            return true;
        }

        public int GetHashCode(TypeReference obj)
        {
            obj = obj.ResolveVirtualElementType();
            if (obj == null)
            {
                return 0;
            }

            int hash = 0;
            if (obj is GenericParameter gpo)
            {
                if (gpo.Owner is TypeReference oo)
                {
                    hash ^= oo.GetElementType().GetHashCode_();
                }
                else if (gpo.Owner is MethodReference omo)
                {
                    hash ^= omo.GetElementMethod().GetHashCode_();
                }
                else
                {
                    hash ^= gpo.Owner.GetHashCode();
                }

                hash ^= gpo.Position;
                return hash;
            }

            if (obj.IsGenericDefinition())
            {
                hash ^= GetHashCode(obj.DeclaringType?.GetElementType());
                hash ^= obj.GenericParameters.Count;
                return hash;
            }

            hash ^= GetHashCode(obj.DeclaringType);
            hash ^= obj.Name.GetHashCode();
            if (obj is GenericInstanceType go)
            {
                foreach (var g in go.GenericArguments)
                {
                    hash ^= GetHashCode(g);
                }
                return hash;
            }

            if (obj is ArrayType ao)
            {
                hash ^= ao.Rank;
                hash ^= GetHashCode(ao.ElementType);
                return hash;
            }

            return hash;
        }
    }
}
