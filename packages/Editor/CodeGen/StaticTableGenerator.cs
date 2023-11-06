using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class StaticTableGenerator : IDisposable
    {
        private Dictionary<(MethodReference, IEnumerable<Instruction>), FieldReference> _staticFields = new Dictionary<(MethodReference, IEnumerable<Instruction>), FieldReference>(MethodCallComparer.Default);
        private TypeDefinition _staticTableType;
        private MethodDefinition _staticTableConstructor;
        public MethodDefinition Constructor => _staticTableConstructor;

        public StaticTableGenerator(ModuleDefinition module, string @namespace, string @class)
        {
            _staticTableType = new TypeDefinition(@namespace, @class, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            _staticTableType.BaseType = module.TypeSystem.Object;

            var cctorAttr = MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            _staticTableConstructor = new MethodDefinition(".cctor", cctorAttr, module.TypeSystem.Void);
            _staticTableType.Methods.Add(_staticTableConstructor);
            module.Types.Add(_staticTableType);
        }

        public void Dispose()
        {
            var ilProcessor = _staticTableConstructor.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ret);
        }

        public Instruction LoadValue(MethodReference method, IEnumerable<Instruction> args)
        {
            var field = GetField(method, args, (m, a, objType, field, body) =>
            {
                var ilProcessor = body.GetILProcessor();
                foreach (var arg in a)
                {
                    ilProcessor.Append(arg.Clone());
                }
                ilProcessor.Emit(OpCodes.Call, m);
                ilProcessor.Emit(OpCodes.Stsfld, field);
            });

            return Instruction.Create(OpCodes.Ldsfld, field);
        }

        public FieldReference GetField(MethodReference method, IEnumerable<Instruction> args, Action<MethodReference, IEnumerable<Instruction>, TypeReference, FieldReference, MethodBody> initialize)
        {
            var key = (method, args);
            if (_staticFields.TryGetValue(key, out FieldReference value))
            {
                return value;
            }

            // インポート
            var objType = _staticTableType.Module.ImportReference(method.ReturnType);

            // 初期値相当のメンバ変数を作成
            var field = new FieldDefinition($"${_staticFields.Count}", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, objType);
            _staticTableType.Fields.Add(field);

            // 静的コンストラクタに初期化処理を書く
            var body = _staticTableConstructor.Body;
            initialize?.Invoke(method, args, objType, field, body);

            // メンバ変数情報をテーブルに保持
            value = field;
            _staticFields.Add(key, value);
            return value;
        }
    }
}
