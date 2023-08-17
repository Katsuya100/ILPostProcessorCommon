using Katuusagi.ILPostProcessorCommon.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class ConstTableGenerator : IDisposable
    {
        private Dictionary<object, FieldReference> _constFields = new Dictionary<object, FieldReference>(new FieldsComparer());
        private TypeDefinition _constTableType;
        private MethodDefinition _constTableConstructor;

        private class FieldsComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object x, object y)
            {
                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                if (x is IReadOnlyArray xe &&
                    y is IReadOnlyArray ye)
                {
                    return xe.Cast<object>().SequenceEqual(ye.Cast<object>());
                }

                return x.Equals(y);
            }

            int IEqualityComparer<object>.GetHashCode(object obj)
            {
                if (!(obj is IReadOnlyArray oe))
                {
                    return obj.GetHashCode();
                }

                const int prime = 31;
                int hash = 1;

                foreach (object o in oe)
                {
                    hash = hash * prime + o.GetHashCode();
                }

                return hash;
            }
        }

        public ConstTableGenerator(ModuleDefinition module, string @namespace, string @class)
        {
            _constTableType = new TypeDefinition(@namespace, @class, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            _constTableType.BaseType = module.TypeSystem.Object;

            var cctorAttr = MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            _constTableConstructor = new MethodDefinition(".cctor", cctorAttr, module.TypeSystem.Void);
            _constTableType.Methods.Add(_constTableConstructor);
            module.Types.Add(_constTableType);
        }

        public void Dispose()
        {
            _constTableConstructor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }

        public Instruction LoadValue(object constantValue)
        {
            var loadLiteral = ILPostProcessorUtils.LoadLiteral(constantValue);
            if (loadLiteral != null)
            {
                return loadLiteral;
            }

            if (constantValue is IReadOnlyArray array)
            {
                var field = GetReadOnlyArrayField(array);
                return Instruction.Create(OpCodes.Ldsfld, field);
            }

            if (constantValue != null)
            {
                var valueType = constantValue.GetType();
                if (!valueType.IsClass && !valueType.IsInterface)
                {
                    var field = GetStructField(constantValue);
                    return Instruction.Create(OpCodes.Ldsfld, field);
                }
            }

            return null;
        }

        public FieldReference GetStructField(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var objType = obj.GetType();
            if (!ILPostProcessorUtils.IsStructRecursive(objType))
            {
                return null;
            }

            return GetField(obj, (o, objType, field, instructions) =>
            {
                int size = Marshal.SizeOf(o);
                byte[] array = new byte[size];
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(o, ptr, false);
                Marshal.Copy(ptr, array, 0, size);
                Marshal.FreeHGlobal(ptr);

                // 静的コンストラクタに初期化処理を書く
                instructions.Add(ILPostProcessorUtils.LoadLiteral(array.Length));
                instructions.Add(Instruction.Create(OpCodes.Conv_U));
                instructions.Add(Instruction.Create(OpCodes.Localloc));
                instructions.Add(Instruction.Create(OpCodes.Dup));
                instructions.Add(ILPostProcessorUtils.LoadLiteral(array[0]));
                instructions.Add(Instruction.Create(OpCodes.Stind_I1));

                for (int i = 1; i < array.Length; ++i)
                {
                    instructions.Add(Instruction.Create(OpCodes.Dup));
                    instructions.Add(ILPostProcessorUtils.LoadLiteral(i));
                    instructions.Add(Instruction.Create(OpCodes.Add));
                    instructions.Add(ILPostProcessorUtils.LoadLiteral(array[i]));
                    instructions.Add(Instruction.Create(OpCodes.Stind_I1));
                }

                instructions.Add(Instruction.Create(OpCodes.Ldobj, objType));
                instructions.Add(Instruction.Create(OpCodes.Stsfld, field));
            });
        }

        public FieldReference GetReadOnlyArrayField(IReadOnlyArray array)
        {
            if (array == null)
            {
                return null;
            }

            var arrayType = array.GetType();
            if (!arrayType.IsGenericType ||
                arrayType.GetGenericTypeDefinition() != typeof(ReadOnlyArray<>))
            {
                return null;
            }

            return GetField(array, (o, objType, field, instructions) =>
            {
                var a = o as IReadOnlyArray;

                var elementType = arrayType.GetGenericArguments()[0];
                var elementTypeRef = _constTableType.Module.ImportReference(elementType);
                var implicitMethod = typeof(ReadOnlyArrayUtils).GetMethod("ConvertArrayToReadOnlyArray", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).MakeGenericMethod(elementType);
                var arrayTypeCast = _constTableType.Module.ImportReference(implicitMethod);

                var count = Count(array);
                instructions.Add(ILPostProcessorUtils.LoadLiteral(count));
                instructions.Add(Instruction.Create(OpCodes.Newarr, elementTypeRef));

                int i = 0;
                foreach (var e in array)
                {
                    instructions.Add(Instruction.Create(OpCodes.Dup));
                    instructions.Add(ILPostProcessorUtils.LoadLiteral(i));
                    instructions.Add(ILPostProcessorUtils.LoadLiteral(e));
                    instructions.Add(ILPostProcessorUtils.SetElement(elementType));
                    ++i;
                }

                instructions.Add(Instruction.Create(OpCodes.Call, arrayTypeCast));
                instructions.Add(Instruction.Create(OpCodes.Stsfld, field));
            });
        }

        public FieldReference GetField(object obj, Action<object, TypeReference, FieldReference, Collection<Instruction>> initialize)
        {
            if (_constFields.TryGetValue(obj, out FieldReference value))
            {
                return value;
            }

            // インポート
            var objType = _constTableType.Module.ImportReference(obj.GetType());

            // 初期値相当のメンバ変数を作成
            var field = new FieldDefinition($"${_constFields.Count}", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, objType);
            _constTableType.Fields.Add(field);

            // 静的コンストラクタに初期化処理を書く
            var instructions = _constTableConstructor.Body.Instructions;
            initialize?.Invoke(obj, objType, field, instructions);

            // メンバ変数情報をテーブルに保持
            value = field;
            _constFields.Add(obj, value);
            return value;
        }

        private int Count(IEnumerable enumerable)
        {
            int c = 0;
            foreach (var e in enumerable)
            {
                ++c;
            }
            return c;
        }
    }
}
