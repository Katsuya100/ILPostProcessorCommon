using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public static readonly TypeReferenceComparer Default = new TypeReferenceComparer();
        internal static readonly TypeReferenceComparer SkipGenericMethodOwner = new TypeReferenceComparer() { _isSkipGenericMethodOwner = true };

        private bool _isSkipGenericMethodOwner = false;

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

            if (x.GetType() == y.GetType())
            {
                if (x is GenericParameter gpx && y is GenericParameter gpy)
                {
                    if (gpx.Owner != gpy.Owner)
                    {
                        if (gpx.Owner is TypeReference ox)
                        {
                            if (!(gpy.Owner is TypeReference oy))
                            {
                                return false;
                            }

                            if (!Equals(ox, oy))
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

                            if (!_isSkipGenericMethodOwner &&
                                !omx.Is(omy))
                            {
                                return false;
                            }
                        }
                    }

                    return gpx.Position == gpy.Position;
                }
                else if (x is TypeSpecification sx && y is TypeSpecification sy)
                {
                    if (x is GenericInstanceType gx && y is GenericInstanceType gy)
                    {
                        if (!gx.GenericArguments.SequenceEqual(gy.GenericArguments, this))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (x is FunctionPointerType fpx && y is FunctionPointerType fpy)
                        {
                            if (!Equals(fpx.ReturnType, fpy.ReturnType))
                            {
                                return false;
                            }

                            return fpx.Parameters.Select(v => v.ParameterType).SequenceEqual(fpy.Parameters.Select(v => v.ParameterType), this);
                        }
                        else if (Equals(sx.ElementType, sy.ElementType))
                        {
                            if (x is ArrayType ax && y is ArrayType ay)
                            {
                                if (ax.IsVector != ay.IsVector)
                                {
                                    return false;
                                }

                                if (!ax.Dimensions.SequenceEqual(ay.Dimensions))
                                {
                                    return false;
                                }

                                return ax.Rank == ay.Rank;
                            }
                            else if (x is RequiredModifierType rpx && y is RequiredModifierType rpy)
                            {
                                return Equals(rpx.ModifierType, rpy.ModifierType);
                            }
                            else if (x is OptionalModifierType opx && y is OptionalModifierType opy)
                            {
                                return Equals(opx.ModifierType, opy.ModifierType);
                            }

                            return true;
                        }

                        return false;
                    }
                }
            }

            if (x.Namespace != y.Namespace)
            {
                return false;
            }

            if (x.Name != y.Name)
            {
                return false;
            }

            var xd = x.GetDeclaringType();
            var yd = y.GetDeclaringType();
            if (!Equals(xd, yd))
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
                return x.GenericParameters.Count == y.GenericParameters.Count;
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
                    hash ^= GetHashCode(oo.GetElementType());
                }
                else if (!_isSkipGenericMethodOwner &&
                         gpo.Owner is MethodReference omo)
                {
                    hash ^= omo.GetElementMethod().GetHashCode_();
                }

                hash ^= gpo.Position;
                return hash;
            }
            else if (obj is TypeSpecification so)
            {
                hash ^= so.GetType().GetHashCode();
                if (so is GenericInstanceType go)
                {
                    foreach (var g in go.GenericArguments)
                    {
                        hash ^= GetHashCode(g);
                    }
                }
                else
                {
                    if (so is FunctionPointerType fpo)
                    {
                        hash ^= GetHashCode(fpo.ReturnType);
                        foreach (var p in fpo.Parameters)
                        {
                            hash ^= GetHashCode(p.ParameterType);
                        }
                        return hash;
                    }

                    hash ^= GetHashCode(so.ElementType);
                    if (so is ArrayType ao)
                    {
                        hash ^= ao.IsVector.GetHashCode();
                        foreach (var dimension in ao.Dimensions)
                        {
                            hash ^= dimension.GetHashCode();
                        }
                        hash ^= ao.Rank;
                    }

                    return hash;
                }
            }

            hash ^= GetHashCode(obj.GetDeclaringType());
            hash ^= obj.Name.GetHashCode();

            if (obj.IsGenericDefinition())
            {
                hash ^= obj.GenericParameters.Count;
                return hash;
            }

            return hash;
        }
    }
}
