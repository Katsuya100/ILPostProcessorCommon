using Katuusagi.ILPostProcessorCommon.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class ConstTableGenerator : IDisposable
    {
        private class CopyObject
        {
            public byte First;
        }

        private Dictionary<object, FieldReference> _constFields = new Dictionary<object, FieldReference>(LiteralComparer.Default);
        private TypeDefinition _constTableType;
        private MethodDefinition _constTableConstructor;

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
                if (valueType.IsValueType)
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
                ref var b = ref UnsafeUtility.As<object, CopyObject>(ref o);
                unsafe
                {
                    var array = (byte*)UnsafeUtility.AddressOf(ref b.First);

                    // 静的コンストラクタに初期化処理を書く
                    instructions.Add(ILPostProcessorUtils.LoadLiteral(size));
                    instructions.Add(Instruction.Create(OpCodes.Conv_U));
                    instructions.Add(Instruction.Create(OpCodes.Localloc));
                    instructions.Add(Instruction.Create(OpCodes.Dup));
                    instructions.Add(ILPostProcessorUtils.LoadLiteral(array[0]));
                    instructions.Add(Instruction.Create(OpCodes.Stind_I1));

                    for (int i = 1; i < size; ++i)
                    {
                        instructions.Add(Instruction.Create(OpCodes.Dup));
                        instructions.Add(ILPostProcessorUtils.LoadLiteral(i));
                        instructions.Add(Instruction.Create(OpCodes.Add));
                        instructions.Add(ILPostProcessorUtils.LoadLiteral(array[i]));
                        instructions.Add(Instruction.Create(OpCodes.Stind_I1));
                    }

                    instructions.Add(Instruction.Create(OpCodes.Ldobj, objType));
                    instructions.Add(Instruction.Create(OpCodes.Stsfld, field));
                }
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
