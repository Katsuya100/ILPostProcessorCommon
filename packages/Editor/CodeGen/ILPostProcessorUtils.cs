using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public static class ILPostProcessorUtils
    {
        public static Logger Logger { get; set; }

        public static AssemblyDefinition LoadAssemblyDefinition(ICompiledAssembly compiledAssembly)
        {
            var resolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData.ToArray()),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = resolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate,
                ReadSymbols = true,
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData.ToArray());
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

            resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }

        public static void ResolveInstructionOpCode(IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions.Reverse())
            {
                if (!(instruction.Operand is Instruction target))
                {
                    continue;
                }

                var isShortSize = IsShortSize(instruction, target);
                if (isShortSize)
                {
                    instruction.OpCode = SwitchLongOpCode(instruction.OpCode);
                }
                else
                {
                    instruction.OpCode = SwitchShortOpCode(instruction.OpCode);
                }
            }
        }

        public static void ReplaceTarget(ILProcessor ilProcessor, Instruction oldTarget, Instruction newTarget)
        {
            var instructions = ilProcessor.Body.Instructions;
            foreach (var instruction in instructions)
            {
                {
                    if (instruction.Operand is Instruction target &&
                        target == oldTarget)
                    {
                        instruction.Operand = newTarget;
                    }
                }

                if (instruction.Operand is Instruction[] targets)
                {
                    for (int i = 0; i < targets.Length; ++i)
                    {
                        if (targets[i] == oldTarget)
                        {
                            targets[i] = newTarget;
                        }
                    }
                }
            }

            foreach (var e in ilProcessor.Body.ExceptionHandlers)
            {
                if (e.TryStart == oldTarget)
                {
                    e.TryStart = newTarget;
                }

                if (e.TryEnd == oldTarget)
                {
                    e.TryEnd = newTarget;
                }

                if (e.FilterStart == oldTarget)
                {
                    e.FilterStart = newTarget;
                }

                if (e.HandlerStart == oldTarget)
                {
                    e.HandlerStart = newTarget;
                }

                if (e.HandlerEnd == oldTarget)
                {
                    e.HandlerEnd = newTarget;
                }
            }
        }

        public static IEnumerable<TypeDefinition> GetAllTypes(this IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types)
            {
                yield return type;
                foreach (var nested in type.NestedTypes.GetAllTypes())
                {
                    yield return nested;
                }
            }
        }

        public static IEnumerable<T> WhereHasAttribute<T>(this IEnumerable<T> self, TypeReference attribute)
            where T : Mono.Cecil.ICustomAttributeProvider
        {
            return self.Where(v => v.HasAttribute(attribute));
        }

        public static IEnumerable<T> WhereHasAttribute<T>(this IEnumerable<T> self, string attribute)
            where T : Mono.Cecil.ICustomAttributeProvider
        {
            return self.Where(v => v.HasAttribute(attribute));
        }

        public static bool HasAttribute(this Mono.Cecil.ICustomAttributeProvider self, TypeReference attribute)
        {
            return self.GetAttribute(attribute) != null;
        }

        public static bool HasAttribute(this Mono.Cecil.ICustomAttributeProvider self, string attribute)
        {
            return self.GetAttribute(attribute) != null;
        }

        public static CustomAttribute GetAttribute(this Mono.Cecil.ICustomAttributeProvider self, TypeReference attribute)
        {
            return self.CustomAttributes.FirstOrDefault(v => v.AttributeType == attribute);
        }

        public static CustomAttribute GetAttribute(this Mono.Cecil.ICustomAttributeProvider self, string attribute)
        {
            return self.CustomAttributes.FirstOrDefault(v => v.AttributeType.FullName == attribute);
        }

        public static bool IsShortSize(Instruction l, Instruction r)
        {
            var diff = CalcOffsetDiff(l, r);
            return -128 <= diff && diff < 128;
        }

        public static int CalcOffsetDiff(Instruction l, Instruction r)
        {
            int size = 0;
            for (var it = l.Next; it != r; it = it.Next)
            {
                if (it == null)
                {
                    size = -1;
                    break;
                }
                size += it.GetSize();
            }

            if (size == -1)
            {
                size = 0;
                for (var it = l; it != r; it = it.Previous)
                {
                    if (it == null)
                    {
                        return 0;
                    }
                    size -= it.GetSize();
                }
            }

            return size;
        }

        public static Instruction SetElement(Type elementType)
        {
            if (elementType.IsEnum)
            {
                elementType = Enum.GetUnderlyingType(elementType);
            }

            if (elementType == typeof(bool) ||
                elementType == typeof(sbyte) ||
                elementType == typeof(byte))
            {
                return Instruction.Create(OpCodes.Stelem_I1);
            }
            if (elementType == typeof(short) ||
                elementType == typeof(ushort) ||
                elementType == typeof(char))
            {
                return Instruction.Create(OpCodes.Stelem_I2);
            }
            if (elementType == typeof(int) ||
                elementType == typeof(uint))
            {
                return Instruction.Create(OpCodes.Stelem_I4);
            }
            if (elementType == typeof(long) ||
                elementType == typeof(ulong))
            {
                return Instruction.Create(OpCodes.Stelem_I8);
            }
            if (elementType == typeof(float))
            {
                return Instruction.Create(OpCodes.Stelem_R4);
            }
            if (elementType == typeof(double))
            {
                return Instruction.Create(OpCodes.Stelem_R8);
            }
            if (elementType.IsClass || elementType.IsInterface)
            {
                return Instruction.Create(OpCodes.Stelem_Ref);
            }

            return null;
        }

        public static Instruction LoadLiteral(object literalValue)
        {
            var literalType = literalValue.GetType();
            if (literalValue is Enum enumValue)
            {
                var underlyingType = Enum.GetUnderlyingType(literalType);
                if (underlyingType == typeof(sbyte))
                {
                    literalValue = (sbyte)(object)enumValue;
                }
                else if (underlyingType == typeof(byte))
                {
                    literalValue = (byte)(object)enumValue;
                }
                else if (underlyingType == typeof(short))
                {
                    literalValue = (short)(object)enumValue;
                }
                else if (underlyingType == typeof(ushort))
                {
                    literalValue = (ushort)(object)enumValue;
                }
                else if (underlyingType == typeof(int))
                {
                    literalValue = (int)(object)enumValue;
                }
                else if (underlyingType == typeof(uint))
                {
                    literalValue = (uint)(object)enumValue;
                }
                else if (underlyingType == typeof(long))
                {
                    literalValue = (long)(object)enumValue;
                }
                else if (underlyingType == typeof(ulong))
                {
                    literalValue = (ulong)(object)enumValue;
                }
            }

            if (literalValue is ulong ulongValue)
            {
                literalValue = (long)ulongValue;
            }

            if (literalValue is long longValue)
            {
                // ‚±‚ê‚ð‚·‚éê‡conv.i8‚à•K—v
                /*
                if (int.MinValue <= longValue && longValue <= int.MaxValue)
                {
                    literalValue = (int)longValue;
                }
                else
                */
                {
                    return Instruction.Create(OpCodes.Ldc_I8, longValue);
                }
            }

            if (literalValue is bool boolValue)
            {
                literalValue = boolValue ? 1 : 0;
            }
            else if (literalValue is byte byteValue)
            {
                literalValue = (int)byteValue;
            }
            else if (literalValue is short shortValue)
            {
                literalValue = (int)shortValue;
            }
            else if (literalValue is ushort ushortValue)
            {
                literalValue = (int)ushortValue;
            }
            else if (literalValue is char charValue)
            {
                literalValue = (int)charValue;
            }
            else if (literalValue is uint uintValue)
            {
                literalValue = (int)uintValue;
            }

            if (literalValue is int intValue)
            {
                switch (intValue)
                {
                    case -1:
                        return Instruction.Create(OpCodes.Ldc_I4_M1);
                    case 0:
                        return Instruction.Create(OpCodes.Ldc_I4_0);
                    case 1:
                        return Instruction.Create(OpCodes.Ldc_I4_1);
                    case 2:
                        return Instruction.Create(OpCodes.Ldc_I4_2);
                    case 3:
                        return Instruction.Create(OpCodes.Ldc_I4_3);
                    case 4:
                        return Instruction.Create(OpCodes.Ldc_I4_4);
                    case 5:
                        return Instruction.Create(OpCodes.Ldc_I4_5);
                    case 6:
                        return Instruction.Create(OpCodes.Ldc_I4_6);
                    case 7:
                        return Instruction.Create(OpCodes.Ldc_I4_7);
                    case 8:
                        return Instruction.Create(OpCodes.Ldc_I4_8);
                }

                if (sbyte.MinValue <= intValue && intValue <= sbyte.MaxValue)
                {
                    literalValue = (sbyte)intValue;
                }
                else
                {
                    return Instruction.Create(OpCodes.Ldc_I4, intValue);
                }
            }

            if (literalValue is sbyte sbyteValue)
            {
                return Instruction.Create(OpCodes.Ldc_I4_S, sbyteValue);
            }

            if (literalValue is double doubleValue)
            {
                return Instruction.Create(OpCodes.Ldc_R8, doubleValue);
            }

            if (literalValue is float floatValue)
            {
                return Instruction.Create(OpCodes.Ldc_R4, floatValue);
            }

            if (literalValue is string stringValue)
            {
                return Instruction.Create(OpCodes.Ldstr, stringValue);
            }

            return null;
        }

        public static bool TryEmulateLiteral<T>(ref Instruction instruction, out T result)
        {
            if (TryEmulateLiteral(ref instruction, out object r) &&
                r is T resultValue)
            {
                result = resultValue;
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryEmulateLiteral(ref Instruction instruction, Type type, out object result)
        {
            if (!TryEmulateLiteral(ref instruction, out result))
            {
                return false;
            }

            if (result is string)
            {
                if (type == typeof(string))
                {
                    return true;
                }

                return false;
            }

            if (result is int intValue)
            {
                if (type == typeof(bool))
                {
                    result = intValue != 0;
                    return true;
                }

                if (type == typeof(sbyte))
                {
                    result = (sbyte)intValue;
                    return true;
                }

                if (type == typeof(byte))
                {
                    result = (byte)intValue;
                    return true;
                }

                if (type == typeof(short))
                {
                    result = (short)intValue;
                    return true;
                }

                if (type == typeof(ushort))
                {
                    result = (ushort)intValue;
                    return true;
                }

                if (type == typeof(int))
                {
                    return true;
                }

                if (type == typeof(uint))
                {
                    result = (uint)intValue;
                    return true;
                }

                if (type == typeof(long))
                {
                    result = (long)intValue;
                    return true;
                }

                if (type == typeof(ulong))
                {
                    result = (ulong)intValue;
                    return true;
                }

                if (type == typeof(float))
                {
                    result = (float)intValue;
                    return true;
                }

                if (type == typeof(double))
                {
                    result = (double)intValue;
                    return true;
                }

                if (type == typeof(char))
                {
                    result = (char)intValue;
                    return true;
                }

                if (type.IsEnum)
                {
                    result = intValue;
                    return true;
                }

                return false;
            }

            if (result is long longValue)
            {
                if (type == typeof(bool))
                {
                    result = longValue != 0;
                    return true;
                }

                if (type == typeof(sbyte))
                {
                    result = (sbyte)longValue;
                    return true;
                }

                if (type == typeof(byte))
                {
                    result = (byte)longValue;
                    return true;
                }

                if (type == typeof(short))
                {
                    result = (short)longValue;
                    return true;
                }

                if (type == typeof(ushort))
                {
                    result = (ushort)longValue;
                    return true;
                }

                if (type == typeof(int))
                {
                    result = (int)longValue;
                    return true;
                }

                if (type == typeof(uint))
                {
                    result = (uint)longValue;
                    return true;
                }

                if (type == typeof(long))
                {
                    return true;
                }

                if (type == typeof(ulong))
                {
                    result = (ulong)longValue;
                    return true;
                }

                if (type == typeof(float))
                {
                    result = (float)longValue;
                    return true;
                }

                if (type == typeof(double))
                {
                    result = (double)longValue;
                    return true;
                }

                if (type == typeof(char))
                {
                    result = (char)longValue;
                    return true;
                }

                if (type.IsEnum)
                {
                    result = longValue;
                    return true;
                }

                return false;
            }

            if (result is float floatValue)
            {
                if (type == typeof(bool))
                {
                    result = floatValue != 0;
                    return true;
                }

                if (type == typeof(sbyte))
                {
                    result = (sbyte)floatValue;
                    return true;
                }

                if (type == typeof(byte))
                {
                    result = (byte)floatValue;
                    return true;
                }

                if (type == typeof(short))
                {
                    result = (short)floatValue;
                    return true;
                }

                if (type == typeof(ushort))
                {
                    result = (ushort)floatValue;
                    return true;
                }

                if (type == typeof(int))
                {
                    result = (int)floatValue;
                    return true;
                }

                if (type == typeof(uint))
                {
                    result = (uint)floatValue;
                    return true;
                }

                if (type == typeof(long))
                {
                    result = (long)floatValue;
                    return true;
                }

                if (type == typeof(ulong))
                {
                    result = (ulong)floatValue;
                    return true;
                }

                if (type == typeof(float))
                {
                    return true;
                }

                if (type == typeof(double))
                {
                    result = (double)floatValue;
                    return true;
                }

                if (type == typeof(char))
                {
                    result = (char)floatValue;
                    return true;
                }

                return false;
            }

            if (result is double doubleValue)
            {
                if (type == typeof(bool))
                {
                    result = doubleValue != 0;
                    return true;
                }

                if (type == typeof(sbyte))
                {
                    result = (sbyte)doubleValue;
                    return true;
                }

                if (type == typeof(byte))
                {
                    result = (byte)doubleValue;
                    return true;
                }

                if (type == typeof(short))
                {
                    result = (short)doubleValue;
                    return true;
                }

                if (type == typeof(ushort))
                {
                    result = (ushort)doubleValue;
                    return true;
                }

                if (type == typeof(int))
                {
                    result = (int)doubleValue;
                    return true;
                }

                if (type == typeof(uint))
                {
                    result = (uint)doubleValue;
                    return true;
                }

                if (type == typeof(long))
                {
                    result = (long)doubleValue;
                    return true;
                }

                if (type == typeof(ulong))
                {
                    result = (ulong)doubleValue;
                    return true;
                }

                if (type == typeof(float))
                {
                    result = (float)doubleValue;
                    return true;
                }

                if (type == typeof(double))
                {
                    return true;
                }

                if (type == typeof(char))
                {
                    result = (char)doubleValue;
                    return true;
                }

                return false;
            }

            return false;
        }

        public static bool TryEmulateLiteral(ref Instruction instruction, out object result)
        {
            if (instruction.OpCode == OpCodes.Ldstr)
            {
                if (instruction.Operand is string)
                {
                    result = instruction.Operand;
                    return true;
                }

                result = instruction.Operand.ToString();
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_0)
            {
                result = 0;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_1)
            {
                result = 1;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_2)
            {
                result = 2;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_3)
            {
                result = 3;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_4)
            {
                result = 4;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_5)
            {
                result = 5;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_6)
            {
                result = 6;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_7)
            {
                result = 7;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_8)
            {
                result = 8;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_M1)
            {
                result = -1;
                return true;
            }
            if (instruction.OpCode == OpCodes.Ldc_I4_S || instruction.OpCode == OpCodes.Ldc_I4)
            {
                if (instruction.Operand is int)
                {
                    result = instruction.Operand;
                    return true;
                }

                result = int.Parse(instruction.Operand.ToString());
                return true;
            }

            if (instruction.OpCode == OpCodes.Ldc_I8)
            {
                if (instruction.Operand is long)
                {
                    result = instruction.Operand;
                    return true;
                }

                result = long.Parse(instruction.Operand.ToString());
                return true;
            }

            if (instruction.OpCode == OpCodes.Ldc_R4)
            {
                if (instruction.Operand is float)
                {
                    result = instruction.Operand;
                    return true;
                }

                result = float.Parse(instruction.Operand.ToString());
                return true;
            }

            if (instruction.OpCode == OpCodes.Ldc_R8)
            {
                if (instruction.Operand is double)
                {
                    result = instruction.Operand;
                    return true;
                }

                result = double.Parse(instruction.Operand.ToString());
                return true;
            }

            if (instruction.OpCode == OpCodes.Conv_I1)
            {
                instruction = instruction.Previous;
                if (!TryEmulateLiteral(ref instruction, out result))
                {
                    return false;
                }

                if (result is int intValue)
                {
                    result = (sbyte)intValue;
                    return true;
                }
                if (result is long longValue)
                {
                    result = (sbyte)longValue;
                    return true;
                }
                if (result is float floatValue)
                {
                    result = (sbyte)floatValue;
                    return true;
                }
                if (result is double doubleValue)
                {
                    result = (sbyte)doubleValue;
                    return true;
                }
            }

            if (instruction.OpCode == OpCodes.Conv_I2)
            {
                instruction = instruction.Previous;
                if (!TryEmulateLiteral(ref instruction, out result))
                {
                    return false;
                }

                if (result is int intValue)
                {
                    result = (short)intValue;
                    return true;
                }
                if (result is long longValue)
                {
                    result = (short)longValue;
                    return true;
                }
                if (result is float floatValue)
                {
                    result = (short)floatValue;
                    return true;
                }
                if (result is double doubleValue)
                {
                    result = (short)doubleValue;
                    return true;
                }
            }

            if (instruction.OpCode == OpCodes.Conv_I4)
            {
                instruction = instruction.Previous;
                if (!TryEmulateLiteral(ref instruction, out result))
                {
                    return false;
                }

                if (result is int intValue)
                {
                    result = intValue;
                    return true;
                }
                if (result is long longValue)
                {
                    result = (int)longValue;
                    return true;
                }
                if (result is float floatValue)
                {
                    result = (int)floatValue;
                    return true;
                }
                if (result is double doubleValue)
                {
                    result = (int)doubleValue;
                    return true;
                }
            }

            if (instruction.OpCode == OpCodes.Conv_I8)
            {
                instruction = instruction.Previous;
                if (!TryEmulateLiteral(ref instruction, out result))
                {
                    return false;
                }

                if (result is int intValue)
                {
                    result = (long)intValue;
                    return true;
                }
                if (result is long longValue)
                {
                    result = longValue;
                    return true;
                }
                if (result is float floatValue)
                {
                    result = (long)floatValue;
                    return true;
                }
                if (result is double doubleValue)
                {
                    result = (long)doubleValue;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public static IEnumerable<MethodInfo> FindMethods<T>(Assembly assembly)
            where T : Attribute
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.GetCustomAttribute<T>() == null)
                    {
                        continue;
                    }

                    yield return method;
                }
            }
        }

        public static string GetTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                string generic = string.Empty;
                foreach (var arg in type.GetGenericArguments())
                {
                    generic += $"{GetTypeName(arg)},";
                }
                generic = generic.Remove(generic.Length - 1, 1);

                string parentName;
                if (type.ReflectedType != null)
                {
                    parentName = $"{GetTypeName(type)}/";
                }
                else
                {
                    parentName = $"{type.Namespace}.";
                }

                return $"{parentName}{type.Name}<{generic}>";
            }

            return type.FullName.Replace("+", "/");
        }

        public static string GetMethodName(MethodInfo method)
        {
            string parameters = string.Empty;
            if (method.GetParameters().Any())
            {
                foreach (var arg in method.GetParameters())
                {
                    parameters += $"{arg.ParameterType.Name},";
                }
            }
            parameters = parameters.Remove(parameters.Length - 1, 1);
            return $"{method.ReflectedType.FullName}.{method.Name}({parameters})";
        }

        public static string GetMethodName(MethodDefinition method)
        {
            string parameters = string.Empty;
            if (method.Parameters.Any())
            {
                foreach (var arg in method.Parameters)
                {
                    parameters += $"{arg.ParameterType.Name},";
                }
                parameters = parameters.Remove(parameters.Length - 1, 1);
            }
            return $"{method.DeclaringType.FullName}.{method.Name}({parameters})";
        }

        public static bool IsStructRecursive(Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }

            if (type.IsClass || type.IsInterface)
            {
                return false;
            }

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return fields.Select(v => v.FieldType).All(IsStructRecursive);
        }

        public static Type GetDelegateType(IEnumerable<Type> args, Type ret = null)
        {
            var count = args.Count();
            Type funcType;
            string typeName;
            if (ret == null || ret == typeof(void))
            {
                typeName = $"System.Action`{count}";
                funcType = Type.GetType(typeName);
                return funcType.MakeGenericType(args.ToArray());
            }
            
            typeName = $"System.Func`{count + 1}";
            funcType = Type.GetType(typeName);
            return funcType.MakeGenericType(args.Append(ret).ToArray());
        }

        public static void LogWarning(object o)
        {
            Logger.LogWarning(o);
        }

        public static void LogWarning(object o, MethodDefinition method, Instruction instruction)
        {
            Logger.LogWarning(o, method, instruction);
        }

        public static void LogWarning(object o, MethodInfo method)
        {
            Logger.LogWarning(o, method);
        }

        public static void LogWarning(object o, string stacktrace)
        {
            Logger.LogWarning(o, stacktrace);
        }

        public static void LogWarning(object o, string file, int line, int column)
        {
            Logger.LogWarning(o, file, line, column);
        }

        public static void LogError(object o)
        {
            Logger.LogError(o);
        }

        public static void LogError(object o, MethodDefinition method, Instruction instruction)
        {
            Logger.LogError(o, method, instruction);
        }

        public static void LogError(object o, MethodInfo method)
        {
            Logger.LogError(o, method);
        }

        public static void LogError(object o, string stacktrace)
        {
            Logger.LogError(o, stacktrace);
        }

        public static void LogError(object o, string file, int line, int column)
        {
            Logger.LogError(o, file, line, column);
        }

        public static void LogException(Exception e)
        {
            Logger.LogException(e);
        }

        public static OpCode SwitchShortOpCode(OpCode opCode)
        {
            if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            else if (opCode == OpCodes.Br_S)
                return OpCodes.Br;
            else if (opCode == OpCodes.Brfalse_S)
                return OpCodes.Brfalse;
            else if (opCode == OpCodes.Brtrue_S)
                return OpCodes.Brtrue;
            else if (opCode == OpCodes.Beq_S)
                return OpCodes.Beq;
            else if (opCode == OpCodes.Bge_S)
                return OpCodes.Bge;
            else if (opCode == OpCodes.Bgt_S)
                return OpCodes.Bgt;
            else if (opCode == OpCodes.Ble_S)
                return OpCodes.Ble;
            else if (opCode == OpCodes.Blt_S)
                return OpCodes.Blt;
            else if (opCode == OpCodes.Bne_Un_S)
                return OpCodes.Bne_Un;
            else if (opCode == OpCodes.Bge_Un_S)
                return OpCodes.Bge_Un;
            else if (opCode == OpCodes.Bgt_Un_S)
                return OpCodes.Bgt_Un;
            else if (opCode == OpCodes.Ble_Un_S)
                return OpCodes.Ble_Un;
            else if (opCode == OpCodes.Blt_Un_S)
                return OpCodes.Blt_Un;
            else if (opCode == OpCodes.Leave_S)
                return OpCodes.Leave;
            return opCode;
        }

        public static OpCode SwitchLongOpCode(OpCode opCode)
        {
            if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave_S;
            else if (opCode == OpCodes.Br)
                return OpCodes.Br_S;
            else if (opCode == OpCodes.Brfalse)
                return OpCodes.Brfalse_S;
            else if (opCode == OpCodes.Brtrue)
                return OpCodes.Brtrue_S;
            else if (opCode == OpCodes.Beq)
                return OpCodes.Beq_S;
            else if (opCode == OpCodes.Bge)
                return OpCodes.Bge_S;
            else if (opCode == OpCodes.Bgt)
                return OpCodes.Bgt_S;
            else if (opCode == OpCodes.Ble)
                return OpCodes.Ble_S;
            else if (opCode == OpCodes.Blt)
                return OpCodes.Blt_S;
            else if (opCode == OpCodes.Bne_Un)
                return OpCodes.Bne_Un_S;
            else if (opCode == OpCodes.Bge_Un)
                return OpCodes.Bge_Un_S;
            else if (opCode == OpCodes.Bgt_Un)
                return OpCodes.Bgt_Un_S;
            else if (opCode == OpCodes.Ble_Un)
                return OpCodes.Ble_Un_S;
            else if (opCode == OpCodes.Blt_Un)
                return OpCodes.Blt_Un_S;
            else if (opCode == OpCodes.Leave)
                return OpCodes.Leave;
            return opCode;
        }
    }
}
