using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class InstructionComparer : IEqualityComparer<Instruction>
    {
        public const int Prime = 31;
        public static readonly InstructionComparer Default = new InstructionComparer();

        public bool Equals(Instruction x, Instruction y)
        {
            if (x.OpCode.Code != y.OpCode.Code ||
                x.OpCode.OperandType != y.OpCode.OperandType)
            {
                return false;
            }

            if (x.Operand == y.Operand ||
                Equals(x.Operand, y.Operand))
            {
                return true;
            }

            if (!(x.Operand is MemberReference xmo) || !(y.Operand is MemberReference ymo))
            {
                return false;
            }

            var result = MemberReferenceComparer.Default.Equals(xmo, ymo);
            return result;
        }

        public int GetHashCode(Instruction obj)
        {
            int hash = obj.OpCode.GetHashCode();
            hash ^= obj.OpCode.OperandType.GetHashCode();
            if (obj.Operand is MemberReference omo)
            {
                hash ^= MemberReferenceComparer.Default.GetHashCode(omo);
            }
            else if (obj.Operand != null)
            {
                hash ^= obj.Operand.GetHashCode();
            }

            return hash;
        }
    }
}
