using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class StaticTableGenerator : IDisposable
    {
        private Dictionary<(MethodReference, IEnumerable<Instruction>), FieldReference> _staticFields = new Dictionary<(MethodReference, IEnumerable<Instruction>), FieldReference>(MethodCallComparer.Default);
        private ModuleDefinition _module;
        private string _namespace;
        private string _class;
        private List<MethodDefinition> _staticTableConstructors = new List<MethodDefinition>();

        public StaticTableGenerator(ModuleDefinition module, string @namespace, string @class)
        {
            _module = module;
            _namespace = @namespace;
            _class = @class;
        }

        public void Dispose()
        {
            foreach (var cctor in _staticTableConstructors)
            {
                var ilProcessor = cctor.Body.GetILProcessor();
                ilProcessor.Emit(OpCodes.Ret);
            }
        }

        public bool IsStaticTableConstructor(MethodReference ctor)
        {
            return _staticTableConstructors.Contains(ctor, MethodReferenceComparer.Default);
        }

        public Instruction LoadValue(MethodReference method, IEnumerable<Instruction> args)
        {
            var field = GetField(method, args, (m, a, objType, field, body) =>
            {
                var ilProcessor = body.GetILProcessor();
                foreach (var arg in a)
                {
                    ilProcessor.Append(arg);
                }
                ilProcessor.Emit(OpCodes.Call, m);
                ilProcessor.Emit(OpCodes.Stsfld, field);
            });

            return Instruction.Create(OpCodes.Ldsfld, field);
        }

        public FieldReference GetField(MethodReference method, IEnumerable<Instruction> args, Action<MethodReference, IEnumerable<Instruction>, TypeReference, FieldReference, MethodBody> initialize)
        {
            var module = _module;
            var resolvedMethod = method.Resolve();

            var genType = method.DeclaringType as GenericInstanceType;
            var genMethod = method as GenericInstanceMethod;

            var isGenericType = genType != null;
            var isGenericMethod = genMethod != null;

            var typeGenCount = isGenericType ? genType.GenericArguments.Count : 0;
            var methodGenCount = isGenericMethod ? genMethod.GenericArguments.Count : 0;

            var isGeneric = isGenericType || isGenericMethod;

            var key = (resolvedMethod, args);
            if (!_staticFields.TryGetValue(key, out FieldReference value))
            {
                var resolvedDeclaringType = resolvedMethod.DeclaringType;
                var instanceMethod = module.ImportReference(resolvedMethod);
                var instanceDeclaringType = module.ImportReference(resolvedDeclaringType);
                var targetType = new TypeDefinition(_namespace, $"{_class}_{_staticFields.Count}", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract, module.TypeSystem.Object);
                module.Types.Add(targetType);

                if (isGenericType)
                {
                    foreach (var gen in instanceDeclaringType.GenericParameters)
                    {
                        var newGen = new GenericParameter(gen.Name, targetType);
                        targetType.GenericParameters.Add(newGen);
                    }
                    instanceDeclaringType = instanceDeclaringType.MakeGenericInstanceType(targetType.GenericParameters);
                }

                instanceMethod.DeclaringType = instanceDeclaringType;
                if (isGenericMethod)
                {
                    var count = targetType.GenericParameters.Count;
                    foreach (var gen in instanceMethod.GenericParameters)
                    {
                        var newGen = new GenericParameter(gen.Name, targetType);
                        targetType.GenericParameters.Add(newGen);
                    }

                    instanceMethod = instanceMethod.MakeGenericInstanceMethod(targetType.GenericParameters.Skip(count));
                }

                var cctorAttr = MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                var cctor = new MethodDefinition(".cctor", cctorAttr, module.TypeSystem.Void);
                targetType.Methods.Add(cctor);

                _staticTableConstructors.Add(cctor);
                var body = cctor.Body;

                // インポート
                var objType = module.ImportReference(instanceMethod.ReturnType);

                // 初期値相当のメンバ変数を作成
                var field = new FieldDefinition($"result", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, objType);
                targetType.Fields.Add(field);

                // メンバ変数情報をテーブルに保持
                value = field;
                args = args.Select(v => v.Clone()).ToArray();
                key.args = args;
                _staticFields.Add(key, value);

                FieldReference f = field;
                if (isGeneric)
                {
                    var dec = f.DeclaringType.MakeGenericInstanceType(f.DeclaringType.GenericParameters);
                    f = module.ImportReference(new FieldReference(field.Name, field.FieldType, dec));
                }

                // 静的コンストラクタに初期化処理を書く
                initialize?.Invoke(instanceMethod, args, objType, f, body);
            }

            if (isGeneric)
            {
                TypeReference[] t = new TypeReference[typeGenCount + methodGenCount];

                int i = 0;
                if (genType != null)
                {
                    foreach (var gen in genType.GenericArguments)
                    {
                        t[i++] = module.ImportReference(gen);
                    }
                }
                if (genMethod != null)
                {
                    foreach (var gen in genMethod.GenericArguments)
                    {
                        t[i++] = module.ImportReference(gen);
                    }
                }

                var declaringType = value.DeclaringType.MakeGenericInstanceType(t);
                var newDeclaringType = module.ImportReference(declaringType);
                value = new FieldReference(value.Name, value.FieldType, newDeclaringType);
            }

            return value;
        }
    }
}
