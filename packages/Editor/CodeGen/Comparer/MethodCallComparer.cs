using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class MethodCallComparer : IEqualityComparer<(MethodReference, IEnumerable<Instruction>)>
    {
        public const int Prime = 31;
        public static readonly MethodCallComparer Default = new MethodCallComparer();

        public bool Equals((MethodReference, IEnumerable<Instruction>) x, (MethodReference, IEnumerable<Instruction>) y)
        {
            if (!x.Item1.Is(y.Item1))
            {
                return false;
            }

            return x.Item2.SequenceEqual(y.Item2, InstructionComparer.Default);
        }

        public int GetHashCode((MethodReference, IEnumerable<Instruction>) obj)
        {
            var hash = 1;

            hash = hash * Prime + obj.Item1.GetHashCode_();
            foreach (Instruction o in obj.Item2)
            {
                hash = hash * Prime + InstructionComparer.Default.GetHashCode(o);
            }

            return hash;
        }
    }
}
