using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public static class ILPPUtils
    {
        [ThreadStatic]
        private static Logger _logger;
        public static Logger Logger => _logger;

        public static void InitLog<T>(ICompiledAssembly assembly)
        {
            _logger = new Logger(typeof(T), assembly);
        }

        public static void Log(object o)
        {
            _logger.Log(0, o);
        }

        public static void LogWarning(object log)
        {
            _logger.Log(DiagnosticType.Warning, log);
        }

        public static void LogWarning(string id, string title, object log)
        {
            _logger.Log(DiagnosticType.Warning, id, title, log);
        }

        public static void LogWarning(string id, string title, object log, MethodDefinition method, Instruction instruction)
        {
            _logger.Log(DiagnosticType.Warning, id, title, log, method, instruction);
        }

        public static void LogWarning(string id, string title, object log, MemberReference member)
        {
            _logger.Log(DiagnosticType.Warning, id, title, log, member);
        }

        public static void LogWarning(string id, string title, object log, System.Reflection.MemberInfo member)
        {
            _logger.Log(DiagnosticType.Warning, id, title, log, member);
        }

        public static void LogWarning(string id, string title, object log, SequencePoint point)
        {
            _logger.Log(DiagnosticType.Warning, id, title, log, point);
        }

        public static void LogWarning(string id, string title, object log, string file, int line, int column)
        {
            _logger.Log(DiagnosticType.Warning, id, title, log, file, line, column);
        }

        public static void LogError(object log)
        {
            _logger.Log(DiagnosticType.Error, log);
        }

        public static void LogError(string id, string title, object log)
        {
            _logger.Log(DiagnosticType.Error, id, title, log);
        }

        public static void LogError(string id, string title, object log, MethodDefinition method, Instruction instruction)
        {
            _logger.Log(DiagnosticType.Error, id, title, log, method, instruction);
        }

        public static void LogError(string id, string title, object log, MemberReference member)
        {
            _logger.Log(DiagnosticType.Error, id, title, log, member);
        }

        public static void LogError(string id, string title, object log, System.Reflection.MemberInfo member)
        {
            _logger.Log(DiagnosticType.Error, id, title, log, member);
        }

        public static void LogError(string id, string title, object log, SequencePoint point)
        {
            _logger.Log(DiagnosticType.Error, id, title, log, point);
        }

        public static void LogError(string id, string title, object log, string file, int line, int column)
        {
            _logger.Log(DiagnosticType.Error, id, title, log, file, line, column);
        }

        public static void LogException(Exception e)
        {
            _logger.Log(DiagnosticType.Error, e);
        }

        public static AssemblyDefinition LoadAssemblyDefinition(ICompiledAssembly compiledAssembly)
        {
            var resolver = new PostProcessorAssemblyResolver(compiledAssembly);
            var readerParameters = new ReaderParameters()
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
                    instruction.OpCode = SwitchToShortJump(instruction.OpCode);
                }
                else
                {
                    instruction.OpCode = SwitchToLongJump(instruction.OpCode);
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

        public static IEnumerable<Type> GetAllTypes(this IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                yield return type;
                foreach (var nested in type.GetNestedTypes().GetAllTypes())
                {
                    yield return nested;
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
            if (!elementType.IsValueType)
            {
                return Instruction.Create(OpCodes.Stelem_Ref);
            }

            return null;
        }

        public static Instruction LoadLiteral(object literalValue)
        {
            if (literalValue == null)
            {
                return Instruction.Create(OpCodes.Ldnull);
            }

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
                // これをする場合conv.i8も必要
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
        public static Instruction LoadArgument(ParameterDefinition parameter)
        {
            switch (parameter.Index)
            {
                case 0:
                    return Instruction.Create(OpCodes.Ldarg_0);
                case 1:
                    return Instruction.Create(OpCodes.Ldarg_1);
                case 2:
                    return Instruction.Create(OpCodes.Ldarg_2);
                case 3:
                    return Instruction.Create(OpCodes.Ldarg_3);
            }

            if (parameter.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Ldarg_S, parameter);
            }

            return Instruction.Create(OpCodes.Ldarg, parameter);
        }

        public static Instruction LoadArgumentAddress(ParameterDefinition parameter)
        {
            if (parameter.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Ldarga_S, parameter);
            }

            return Instruction.Create(OpCodes.Ldarga, parameter);
        }

        public static Instruction SetArgument(ParameterDefinition parameter)
        {
            if (parameter.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Starg_S, parameter);
            }

            return Instruction.Create(OpCodes.Starg, parameter);
        }

        public static Instruction LoadLocal(VariableDefinition variable)
        {
            switch (variable.Index)
            {
                case 0:
                    return Instruction.Create(OpCodes.Ldloc_0);
                case 1:
                    return Instruction.Create(OpCodes.Ldloc_1);
                case 2:
                    return Instruction.Create(OpCodes.Ldloc_2);
                case 3:
                    return Instruction.Create(OpCodes.Ldloc_3);
            }

            if (variable.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Ldloc_S, variable);
            }

            return Instruction.Create(OpCodes.Ldloc, variable);
        }

        public static Instruction LoadLocalAddress(VariableDefinition variable)
        {
            if (variable.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Ldloca_S, variable);
            }

            return Instruction.Create(OpCodes.Ldloca, variable);
        }

        public static Instruction SetLocal(VariableDefinition variable)
        {
            switch (variable.Index)
            {
                case 0:
                    return Instruction.Create(OpCodes.Stloc_0);
                case 1:
                    return Instruction.Create(OpCodes.Stloc_1);
                case 2:
                    return Instruction.Create(OpCodes.Stloc_2);
                case 3:
                    return Instruction.Create(OpCodes.Stloc_3);
            }

            if (variable.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Stloc_S, variable);
            }

            return Instruction.Create(OpCodes.Stloc, variable);
        }

        public static bool TryCast(Type type, object value, out object result)
        {
            result = value;
            if (value == null)
            {
                return !type.IsValueType;
            }

            var valueType = value.GetType();
            if (type.IsAssignableFrom(valueType))
            {
                return true;
            }

            if (value is int intValue)
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

            if (value is long longValue)
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

            if (value is float floatValue)
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

            if (value is double doubleValue)
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

                if (type == typeof(char))
                {
                    result = (char)doubleValue;
                    return true;
                }

                return false;
            }

            return false;
        }

        public static bool TryGetConstValue<T>(this Instruction instruction, out T value, List<Instruction> instructions = null)
        {
            if (instruction.TryGetConstValue(typeof(T), out object r, instructions) &&
                r is T resultValue)
            {
                value = resultValue;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetConstValue(this Instruction instruction, Type type, out object value, List<Instruction> instructions = null)
        {
            if (!instruction.TryGetConstValue(out value, instructions))
            {
                return false;
            }

            return TryCast(type, value, out value);
        }

        public static bool TryGetConstValue(this Instruction instruction, out object value, List<Instruction> instructions = null)
        {
            var opCode = instruction.OpCode;
            var operand = instruction.Operand;
            if (opCode == OpCodes.Ldnull)
            {
                instructions?.Add(instruction);
                value = null;
                return true;
            }

            if (opCode == OpCodes.Ldstr)
            {
                instructions?.Add(instruction);
                if (operand is string)
                {
                    value = operand;
                    return true;
                }

                value = operand.ToString();
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_0)
            {
                instructions?.Add(instruction);
                value = 0;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_1)
            {
                instructions?.Add(instruction);
                value = 1;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_2)
            {
                instructions?.Add(instruction);
                value = 2;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_3)
            {
                instructions?.Add(instruction);
                value = 3;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_4)
            {
                instructions?.Add(instruction);
                value = 4;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_5)
            {
                instructions?.Add(instruction);
                value = 5;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_6)
            {
                instructions?.Add(instruction);
                value = 6;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_7)
            {
                instructions?.Add(instruction);
                value = 7;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_8)
            {
                instructions?.Add(instruction);
                value = 8;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_M1)
            {
                instructions?.Add(instruction);
                value = -1;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_S || opCode == OpCodes.Ldc_I4)
            {
                instructions?.Add(instruction);
                if (operand is int)
                {
                    value = operand;
                    return true;
                }

                value = int.Parse(operand.ToString());
                return true;
            }

            if (opCode == OpCodes.Ldc_I8)
            {
                instructions?.Add(instruction);
                if (operand is long)
                {
                    value = operand;
                    return true;
                }

                value = long.Parse(operand.ToString());
                return true;
            }

            if (opCode == OpCodes.Ldc_R4)
            {
                instructions?.Add(instruction);
                if (operand is float)
                {
                    value = operand;
                    return true;
                }

                value = float.Parse(operand.ToString());
                return true;
            }

            if (opCode == OpCodes.Ldc_R8)
            {
                instructions?.Add(instruction);
                if (operand is double)
                {
                    value = operand;
                    return true;
                }

                value = double.Parse(operand.ToString());
                return true;
            }

            if (opCode == OpCodes.Ldtoken)
            {
                instructions?.Add(instruction);
                value = instruction.Operand;
                return true;
            }

            if (opCode == OpCodes.Conv_I1)
            {
                if (!instruction.TryGetStackPushedInstruction(-1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(out value, instructions))
                {
                    return false;
                }

                instructions?.Add(instruction);
                if (value is int intValue)
                {
                    value = (sbyte)intValue;
                    return true;
                }
                if (value is long longValue)
                {
                    value = (sbyte)longValue;
                    return true;
                }
                if (value is float floatValue)
                {
                    value = (sbyte)floatValue;
                    return true;
                }
                if (value is double doubleValue)
                {
                    value = (sbyte)doubleValue;
                    return true;
                }
                return true;
            }

            if (opCode == OpCodes.Conv_I2)
            {
                if (!instruction.TryGetStackPushedInstruction(-1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(out value, instructions))
                {
                    return false;
                }

                instructions?.Add(instruction);
                if (value is int intValue)
                {
                    value = (short)intValue;
                    return true;
                }
                if (value is long longValue)
                {
                    value = (short)longValue;
                    return true;
                }
                if (value is float floatValue)
                {
                    value = (short)floatValue;
                    return true;
                }
                if (value is double doubleValue)
                {
                    value = (short)doubleValue;
                    return true;
                }
                return true;
            }

            if (opCode == OpCodes.Conv_I4)
            {
                if (!instruction.TryGetStackPushedInstruction(-1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(out value, instructions))
                {
                    return false;
                }

                instructions?.Add(instruction);
                if (value is int intValue)
                {
                    value = intValue;
                    return true;
                }
                if (value is long longValue)
                {
                    value = (int)longValue;
                    return true;
                }
                if (value is float floatValue)
                {
                    value = (int)floatValue;
                    return true;
                }
                if (value is double doubleValue)
                {
                    value = (int)doubleValue;
                    return true;
                }
                return true;
            }

            if (opCode == OpCodes.Conv_I8)
            {
                if (!instruction.TryGetStackPushedInstruction(-1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(out value, instructions))
                {
                    return false;
                }

                instructions?.Add(instruction);
                if (value is int intValue)
                {
                    value = (long)intValue;
                    return true;
                }
                if (value is long longValue)
                {
                    value = longValue;
                    return true;
                }
                if (value is float floatValue)
                {
                    value = (long)floatValue;
                    return true;
                }
                if (value is double doubleValue)
                {
                    value = (long)doubleValue;
                    return true;
                }
                return true;
            }

            if (opCode == OpCodes.Box)
            {
                if (!instruction.TryGetStackPushedInstruction(-1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(out value, instructions))
                {
                    return false;
                }

                instructions?.Add(instruction);
                return true;
            }

            if (opCode == OpCodes.Call)
            {
                var method = instruction.Operand as MethodReference;
                if (method.FullName != "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)")
                {
                    value = null;
                    return false;
                }

                if (!instruction.TryGetPushConstArgumentInstructions(0, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(instruction);
                return true;
            }

            if (opCode == OpCodes.Ldsfld)
            {
                var field = instruction.Operand as FieldReference;
                var declaringTypeName = field.DeclaringType.Name;
                if (declaringTypeName != "$$ConstTable" && !declaringTypeName.StartsWith("$$StaticTable_", StringComparison.Ordinal))
                {
                    value = null;
                    return false;
                }

                value = field;
                instructions?.Add(instruction);
                return true;
            }

            value = null;
            return false;
        }

        public static VariableDefinition GetVariableFromStloc(Instruction stloc, MethodBody body)
        {
            var opCode = stloc.OpCode;
            int index;
            if (opCode == OpCodes.Stloc_0)
            {
                index = 0;
            }
            else if (opCode == OpCodes.Stloc_1)
            {
                index = 1;
            }
            else if (opCode == OpCodes.Stloc_2)
            {
                index = 2;
            }
            else if (opCode == OpCodes.Stloc_3)
            {
                index = 3;
            }
            else if (opCode == OpCodes.Stloc_S)
            {
                index = (short)stloc.Operand;
            }
            else if (opCode == OpCodes.Stloc)
            {
                index = (int)stloc.Operand;
            }
            else
            {
                return null;
            }

            return body.Variables[index];
        }

        public static VariableDefinition GetVariableFromLdloc(Instruction ldloc, MethodBody body)
        {
            var opCode = ldloc.OpCode;
            int index;
            if (opCode == OpCodes.Ldloc_0)
            {
                index = 0;
            }
            else if (opCode == OpCodes.Ldloc_1)
            {
                index = 1;
            }
            else if (opCode == OpCodes.Ldloc_2)
            {
                index = 2;
            }
            else if (opCode == OpCodes.Ldloc_3)
            {
                index = 3;
            }
            else if (opCode == OpCodes.Ldloc_S ||
                     opCode == OpCodes.Ldloca_S ||
                     opCode == OpCodes.Ldloc ||
                     opCode == OpCodes.Ldloca)
            {
                return ldloc.Operand as VariableDefinition;
            }
            else
            {
                return null;
            }

            return body.Variables[index];
        }

        public static ParameterDefinition GetArgumentFromStarg(Instruction starg)
        {
            var opCode = starg.OpCode;
            if (opCode == OpCodes.Starg_S ||
                opCode == OpCodes.Starg)
            {
                return starg.Operand as ParameterDefinition;
            }

            return null;
        }

        public static ParameterDefinition GetArgumentFromLdarg(Instruction ldarg, MethodDefinition method)
        {
            var opCode = ldarg.OpCode;
            int index;
            if (opCode == OpCodes.Ldarg_0)
            {
                index = 0;
            }
            else if (opCode == OpCodes.Ldarg_1)
            {
                index = 1;
            }
            else if (opCode == OpCodes.Ldarg_2)
            {
                index = 2;
            }
            else if (opCode == OpCodes.Ldarg_3)
            {
                index = 3;
            }
            else if (opCode == OpCodes.Ldarg_S ||
                     opCode == OpCodes.Ldarga_S ||
                     opCode == OpCodes.Ldarg ||
                     opCode == OpCodes.Ldarga)
            {
                return ldarg.Operand as ParameterDefinition;
            }
            else
            {
                return null;
            }

            return method.Parameters[index];
        }

        public static IEnumerable<System.Reflection.MethodInfo> FindMethods<T>(System.Reflection.Assembly assembly)
            where T : Attribute
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<T>(method) == null)
                    {
                        continue;
                    }

                    yield return method;
                }
            }
        }

        public static string GetMemberName(System.Reflection.MemberInfo member)
        {
            if (member is Type type)
            {
                return GetTypeName(type);
            }

            if (member is System.Reflection.MethodBase method)
            {
                return GetMethodName(method);
            }

            return $"{GetTypeName(member.ReflectedType)}.{member.Name}";
        }

        public static string GetMemberName(MemberReference member)
        {
            if (member == null)
            {
                return string.Empty;
            }

            if (member is TypeReference type)
            {
                return GetTypeName(type);
            }

            if (member is MethodReference method)
            {
                return GetMethodName(method);
            }

            return $"{GetTypeName(member.DeclaringType)}.{member.Name}";
        }

        public static bool IsStatic(this MemberReference member)
        {
            {
                if (member is FieldDefinition field)
                {
                    return field.IsStatic;
                }

                if (member is PropertyDefinition property)
                {
                    return (property.GetMethod?.IsStatic ?? false) ||
                           (property.SetMethod?.IsStatic ?? false) ||
                           (property.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }

                if (member is MethodDefinition method)
                {
                    return method.IsStatic;
                }

                if (member is EventDefinition @event)
                {
                    return (@event.AddMethod?.IsStatic ?? false) ||
                           (@event.RemoveMethod?.IsStatic ?? false) ||
                           (@event.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }
            }
            {
                if (member is FieldReference field)
                {
                    return field.Resolve().IsStatic;
                }

                if (member is PropertyReference property)
                {
                    var p = property.Resolve();
                    return (p.GetMethod?.IsStatic ?? false) ||
                           (p.SetMethod?.IsStatic ?? false) ||
                           (p.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }

                if (member is MethodReference method)
                {
                    return method.Resolve().IsStatic;
                }

                if (member is EventDefinition @event)
                {
                    var e = @event.Resolve();
                    return (e.AddMethod?.IsStatic ?? false) ||
                           (e.RemoveMethod?.IsStatic ?? false) ||
                           (e.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }
            }

            return false;
        }

        public static bool IsPublic(this MemberReference member)
        {
            {
                if (member is TypeDefinition type)
                {
                    return type.IsPublic || type.IsNestedPublic;
                }

                if (member is FieldDefinition field)
                {
                    return field.IsPublic;
                }

                if (member is PropertyDefinition property)
                {
                    
                    return (property.GetMethod?.IsPublic ?? false) ||
                           (property.SetMethod?.IsPublic ?? false) ||
                           (property.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }

                if (member is MethodDefinition method)
                {
                    return method.IsPublic;
                }

                if (member is EventDefinition @event)
                {
                    return (@event.AddMethod?.IsPublic ?? false) ||
                           (@event.RemoveMethod?.IsPublic ?? false) ||
                           (@event.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }
            }
            {
                if (member is TypeReference type)
                {
                    var typeRef = type.Resolve();
                    return typeRef.IsPublic || typeRef.IsNestedPublic;
                }

                if (member is FieldReference field)
                {
                    return field.Resolve().IsPublic;
                }

                if (member is PropertyReference property)
                {
                    var p = property.Resolve();
                    return (p.GetMethod?.IsPublic ?? false) ||
                           (p.SetMethod?.IsPublic ?? false) ||
                           (p.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }

                if (member is MethodReference method)
                {
                    return method.Resolve().IsPublic;
                }

                if (member is EventDefinition @event)
                {
                    var e = @event.Resolve();
                    return (e.AddMethod?.IsPublic ?? false) ||
                           (e.RemoveMethod?.IsPublic ?? false) ||
                           (e.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }
            }

            return false;
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
                    parentName = $"{GetTypeName(type.ReflectedType)}/";
                }
                else
                {
                    parentName = $"{type.Namespace}.";
                }

                return $"{parentName}{type.Name}<{generic}>";
            }

            if (type.FullName == null)
            {
                return type.Name;
            }
            return type.FullName.Replace("+", "/");
        }

        public static string GetTypeName(TypeReference type)
        {
            IEnumerable<TypeReference> genericArguments = null;
            if (type is GenericInstanceType genType)
            {
                genericArguments = genType.GenericArguments;
            }
            else if (type.HasGenericParameters)
            {
                genericArguments = type.GenericParameters;
            }

            if (genericArguments?.Any() ?? false)
            {
                string generic = string.Empty;
                foreach (var arg in genericArguments)
                {
                    generic += $"{GetTypeName(arg)},";
                }
                generic = generic.Remove(generic.Length - 1, 1);

                string parentName;
                var declairing = type.GetDeclaringType();
                if (declairing != null)
                {
                    parentName = $"{GetTypeName(declairing)}/";
                }
                else
                {
                    parentName = $"{type.Namespace}.";
                }

                return $"{parentName}{type.Name}<{generic}>";
            }

            return type.FullName.Replace("+", "/");
        }

        public static string GetMethodName(System.Reflection.MethodBase method)
        {
            string parameters = string.Empty;
            if (method.GetParameters().Any())
            {
                foreach (var arg in method.GetParameters())
                {
                    parameters += $"{arg.ParameterType.Name},";
                }
                parameters = parameters.Remove(parameters.Length - 1, 1);
            }
            return $"{GetTypeName(method.ReflectedType)}.{method.Name}({parameters})";
        }

        public static string GetMethodName(MethodReference method)
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
            return $"{GetTypeName(method.DeclaringType)}.{method.Name}({parameters})";
        }

        public static bool IsStructRecursive(this Type type)
        {
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }

            if (!type.IsValueType)
            {
                return false;
            }

            var fields = type.GetFields( System.Reflection.BindingFlags.Public |  System.Reflection.BindingFlags.NonPublic |  System.Reflection.BindingFlags.Instance);
            return fields.Select(v => v.FieldType).All(IsStructRecursive);
        }

        public static bool IsStructRecursive(this TypeReference self)
        {
            if (self.IsPrimitive)
            {
                return true;
            }

            var type = self.Resolve();
            if (type == null)
            {
                return false;
            }

            if (type.IsEnum)
            {
                return true;
            }

            if (!type.IsValueType)
            {
                return false;
            }

            var fields = self.GetFields().Where(v => !v.IsStatic);
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

        public static MethodReference MakeGenericInstanceMethod(this MethodReference method, IEnumerable<TypeReference> arguments)
        {
            var genericInstanceMethod = new GenericInstanceMethod(method);
            foreach (TypeReference item in arguments)
            {
                genericInstanceMethod.GenericArguments.Add(item);
            }
            return genericInstanceMethod;
        }

        public static GenericInstanceType MakeGenericInstanceType(this TypeReference self, IEnumerable<TypeReference> arguments)
        {
            GenericInstanceType genericInstanceType = new GenericInstanceType(self);
            foreach (TypeReference item in arguments)
            {
                genericInstanceType.GenericArguments.Add(item);
            }

            return genericInstanceType;
        }

        public static IList<TypeReference> GetGenericArguments(this MethodReference methodRef)
        {
            if (!(methodRef is GenericInstanceMethod genMethod))
            {
                return Array.Empty<TypeReference>();
            }

            return genMethod.GenericArguments;
        }

        public static IList<TypeReference> GetGenericArguments(this TypeReference typeRef)
        {
            if (!(typeRef is GenericInstanceType genType))
            {
                return Array.Empty<TypeReference>();
            }

            return genType.GenericArguments;
        }

        public static IEnumerable<TypeReference> GetNestedTypes(this TypeReference typeRef, TypeDefinition type)
        {
            if (!(typeRef is GenericInstanceType genType))
            {
                return type.NestedTypes;
            }

            var genArgs = genType.GenericArguments;
            return type.NestedTypes.Where(v => v.GenericParameters.Count == genArgs.Count)
                                      .Select(v2 => v2.GetElementType().MakeGenericInstanceType(genArgs))
                                      .OfType<TypeReference>();
        }

        public static TypeReference GetDeclaringType(this TypeReference typeRef)
        {
            if (typeRef.DeclaringType == null)
            {
                return null;
            }

            if (typeRef.IsGenericDefinition())
            {
                var def = typeRef.DeclaringType.Resolve();
                if (def == null)
                {
                    return typeRef.DeclaringType.GetElementType();
                }
                return def;
            }

            if (typeRef is GenericInstanceType genType)
            {
                TypeReference type = typeRef.Resolve();
                if (type == null)
                {
                    type = typeRef.GetElementType();
                }

                TypeReference declaringType = type.DeclaringType.Resolve();
                if (declaringType == null)
                {
                    declaringType = type.DeclaringType.GetElementType();
                }

                var genArgs = genType.GenericArguments;
                var declairingGenArgs = genArgs.Take(declaringType.GenericParameters.Count);
                if (!declairingGenArgs.Any())
                {
                    return typeRef.DeclaringType;
                }

                return declaringType.MakeGenericInstanceType(declairingGenArgs);
            }

            return typeRef.DeclaringType;
        }

        public static IEnumerable<FieldDefinition> GetFields(this TypeReference self)
        {
            if (self is GenericInstanceType genType)
            {
                return genType.GetFields();
            }

            if (!(self is TypeDefinition type))
            {
                type = self.Resolve();
            }

            if (type == null)
            {
                return null;
            }

            return type.Fields;
        }

        public static FieldDefinition[] GetFields(this GenericInstanceType self)
        {
            var type = self.Resolve();
            if (type == null)
            {
                return null;
            }

            var fields = type.Fields;
            var result = new FieldDefinition[fields.Count];
            for (int i = 0; i < result.Length; ++i)
            {
                var field = fields[i];
                var fieldType = field.FieldType;
                if (!self.TryReplaceGenericParameter(fieldType, out fieldType))
                {
                    result[i] = field;
                    continue;
                }

                var newField = field.Clone();
                newField.FieldType = fieldType;
                result[i] = newField;
            }

            return result;
        }

        public static bool TryReplaceGenericParameter(this GenericInstanceType typeRef, TypeReference src, out TypeReference result)
        {
            result = src;
            if (src.IsGenericParameter)
            {
                var arguments = typeRef.GenericArguments;
                var parameters = typeRef.ElementType.GenericParameters;
                var data = parameters
                            .Select((p, i) => (p, i))
                            .FirstOrDefault(v => TypeReferenceComparer.Default.Equals(v.p, src));
                if (data.p == null)
                {
                    return false;
                }

                result = arguments[data.i];
                return true;
            }

            if (src.ContainsGenericParameter &&
                src is GenericInstanceType genType)
            {
                bool isReplaced = false;
                using (ThreadStaticArrayPool.Get<TypeReference>(out var genArgs, genType.GenericArguments.Count))
                {
                    for (int i = 0; i < genArgs.Length; ++i)
                    {
                        if (typeRef.TryReplaceGenericParameter(genType.GenericArguments[i], out genArgs[i]))
                        {
                            isReplaced = true;
                        }
                    }

                    if (!isReplaced)
                    {
                        return false;
                    }

                    result = genType.GetDeclaringType().MakeGenericInstanceType(genArgs);
                    return true;
                }
            }

            return false;
        }

        public static bool IsVolatile(this System.Reflection.FieldInfo field)
        {
            return field.GetRequiredCustomModifiers().Contains(typeof(IsVolatile));
        }

        public static bool IsVolatile(this FieldReference self)
        {
            return (self.FieldType is RequiredModifierType modType &&
                    modType.ModifierType.FullName == "System.Runtime.CompilerServices.IsVolatile");
        }

        public static TypeReference GetForceInstancedGenericType(this TypeReference self)
        {
            if (!IsGenericDefinition(self))
            {
                return self;
            }

            return self.MakeGenericInstanceType(self.GenericParameters);
        }

        public static TypeReference ResolveVirtualElementType(this TypeReference self)
        {
            if (!(self is GenericInstanceType genType))
            {
                return self;
            }

            var type = genType.ElementType;
            if (!type.GenericParameters.SequenceEqual(genType.GenericArguments, TypeReferenceComparer.Default))
            {
                return self;
            }

            return type;
        }

        public static MethodReference ResolveVirtualElementMethod(this MethodReference self)
        {
            if (!(self is GenericInstanceMethod genMethod))
            {
                return self;
            }

            var method = genMethod.ElementMethod;
            if (!method.GenericParameters.SequenceEqual(genMethod.GenericArguments, TypeReferenceComparer.Default))
            {
                return self;
            }

            return method;
        }

        public static bool IsGenericDefinition(this TypeReference self)
        {
            return self.HasGenericParameters && !self.IsGenericInstance;
        }

        public static bool IsGenericDefinition(this MethodReference self)
        {
            return self.HasGenericParameters && !self.IsGenericInstance;
        }

        public static bool IsEnum(this TypeReference self)
        {
            var type = self.Resolve();
            if (type == null)
            {
                return false;
            }

            return type.IsEnum;
        }

        public static bool IsString(this TypeReference self)
        {
            var result = self.FullName == "System.String";
            return result;
        }

        public static bool IsStruct(this TypeReference typeRef)
        {
            if (typeRef.IsPrimitive)
            {
                return true;
            }

            if (typeRef.IsGenericParameter)
            {
                return false;
            }

            var typeDef = typeRef.Resolve();
            if (typeDef == null ||
                typeDef.IsEnum ||
                typeDef.IsValueType)
            {
                return true;
            }

            return false;
        }

        public static bool IsSealed(this TypeReference typeRef)
        {
            var typeDef = typeRef.Resolve();
            if (typeDef == null)
            {
                return false;
            }

            if (typeDef.IsSealed)
            {
                return true;
            }

            return false;
        }

        public static void CreateTypeParameters(ModuleDefinition module, TypeReference typeRef, Dictionary<GenericParameter, TypeReference> typeParameter)
        {
            if (!(typeRef is GenericInstanceType genType))
            {
                return;
            }

            var genParams = genType.ElementType.GenericParameters;
            var genArgs = genType.GenericArguments;
            for (int i = 0; i < genParams.Count; ++i)
            {
                var genParam = genParams[i];
                var genArg = ReplaceGeneric(module, genArgs[i], typeParameter);

                typeParameter[genParam] = genArg;
            }

            var resolved = genType.Resolve();
            if (resolved != null)
            {
                genParams = resolved.GenericParameters;
                for (int i = 0; i < genParams.Count; ++i)
                {
                    var genParam = genParams[i];
                    var genArg = ReplaceGeneric(module, genArgs[i], typeParameter);

                    typeParameter[genParam] = genArg;
                }
            }
        }

        public static void CreateTypeParameters(ModuleDefinition module, MethodReference methodRef, Dictionary<GenericParameter, TypeReference> typeParameter)
        {
            CreateTypeParameters(module, methodRef.DeclaringType, typeParameter);
            if (!(methodRef is GenericInstanceMethod genMethod))
            {
                return;
            }

            var genParams = genMethod.ElementMethod.GenericParameters;
            var genArgs = genMethod.GenericArguments;
            for (int i = 0; i < genParams.Count; ++i)
            {
                var genParam = genParams[i];
                var genArg = ReplaceGeneric(module, genArgs[i], typeParameter);

                typeParameter[genParam] = genArg;
            }

            var resolved = genMethod.Resolve();
            if (resolved != null)
            {
                genParams = resolved.GenericParameters;
                for (int i = 0; i < genParams.Count; ++i)
                {
                    var genParam = genParams[i];
                    var genArg = ReplaceGeneric(module, genArgs[i], typeParameter);

                    typeParameter[genParam] = genArg;
                }
            }
        }

        public static TypeReference ReplaceGeneric(ModuleDefinition module, TypeReference type, Dictionary<GenericParameter, TypeReference> typeParameters)
        {
            if (typeParameters == null)
            {
                return Import(module, type);
            }

            if (!type.ContainsGenericParameter)
            {
                return Import(module, type);
            }

            if (type is GenericParameter genParam)
            {
                if (!typeParameters.TryGetValue(genParam, out var replaced))
                {
                    return Import(module, type);
                }

                return Import(module, replaced);
            }

            if (type is GenericInstanceType genType)
            {
                var genArgs = genType.GenericArguments.Select(v => ReplaceGeneric(module, v, typeParameters));
                var elementType = Import(module, genType.GetElementType());
                var maked = elementType.MakeGenericInstanceType(genArgs);
                return Import(module, maked);
            }

            if (type is ArrayType arrayType)
            {
                var elementType = Import(module, ReplaceGeneric(module, arrayType.ElementType, typeParameters));
                var result = elementType.MakeArrayType();
                if (arrayType.IsVector)
                {
                    return result;
                }

                var dimensions = result.Dimensions;
                dimensions.Clear();
                foreach (var dimension in arrayType.Dimensions)
                {
                    dimensions.Add(dimension);
                }

                return result;
            }

            if (type is PointerType pointerType)
            {
                return Import(module, ReplaceGeneric(module, pointerType.ElementType, typeParameters).MakePointerType());
            }

            if (type is ByReferenceType byRefType)
            {
                return Import(module, ReplaceGeneric(module, byRefType.ElementType, typeParameters).MakeByReferenceType());
            }

            if (type is PinnedType pinnedType)
            {
                return Import(module, ReplaceGeneric(module, pinnedType.ElementType, typeParameters).MakePinnedType());
            }

            if (type is SentinelType sentinelType)
            {
                return Import(module, ReplaceGeneric(module, sentinelType.ElementType, typeParameters).MakeSentinelType());
            }

            if (type is RequiredModifierType rmType)
            {
                var elementType = ReplaceGeneric(module, rmType.ElementType, typeParameters);
                var modifierType = ReplaceGeneric(module, rmType.ModifierType, typeParameters);
                return Import(module, elementType.MakeRequiredModifierType(modifierType));
            }

            if (type is OptionalModifierType omType)
            {
                var elementType = ReplaceGeneric(module, omType.ElementType, typeParameters);
                var modifierType = ReplaceGeneric(module, omType.ModifierType, typeParameters);
                return Import(module, elementType.MakeOptionalModifierType(modifierType));
            }

            if (type is FunctionPointerType fpType)
            {
                var result = new FunctionPointerType();

                result.HasThis = fpType.HasThis;
                result.ExplicitThis = fpType.ExplicitThis;
                result.CallingConvention = fpType.CallingConvention;
                result.ReturnType = ReplaceGeneric(module, fpType.ReturnType, typeParameters);
                foreach (var p in fpType.Parameters)
                {
                    var pType = ReplaceGeneric(module, p.ParameterType, typeParameters);
                    var parameter = new ParameterDefinition(p.Name, p.Attributes, pType);
                    parameter.Constant = p.Constant;
                    foreach (var a in parameter.CustomAttributes)
                    {
                        parameter.CustomAttributes.Add(a);
                    }
                    parameter.MarshalInfo = p.MarshalInfo;
                    result.Parameters.Add(parameter);
                }

                return Import(module, result);
            }

            return Import(module, type);
        }

        public static MethodReference ReplaceGeneric(ModuleDefinition module, MethodReference method, Dictionary<GenericParameter, TypeReference> typeParameters)
        {
            if (typeParameters == null)
            {
                return Import(module, method);
            }

            if (!method.ContainsGenericParameter)
            {
                return Import(module, method);
            }

            if (method is GenericInstanceMethod genMethod)
            {
                var elementMethod = genMethod.GetElementMethod();
                if (elementMethod.DeclaringType.ContainsGenericParameter)
                {
                    var declaringType = ReplaceGeneric(module, elementMethod.DeclaringType, typeParameters);
                    var returnType = Import(module, elementMethod.ReturnType);
                    var resolvedElementMethod = new MethodReference(elementMethod.Name, returnType, declaringType);
                    resolvedElementMethod.HasThis = elementMethod.HasThis;
                    resolvedElementMethod.ExplicitThis = elementMethod.ExplicitThis;
                    resolvedElementMethod.CallingConvention = elementMethod.CallingConvention;

                    resolvedElementMethod.MethodReturnType.Attributes = elementMethod.MethodReturnType.Attributes;
                    resolvedElementMethod.MethodReturnType.Constant = elementMethod.MethodReturnType.Constant;
                    foreach (var a in resolvedElementMethod.MethodReturnType.CustomAttributes)
                    {
                        resolvedElementMethod.MethodReturnType.CustomAttributes.Add(a);
                    }
                    resolvedElementMethod.MethodReturnType.MarshalInfo = elementMethod.MethodReturnType.MarshalInfo;

                    foreach (var p in elementMethod.Parameters)
                    {
                        var parameterType = Import(module, p.ParameterType);
                        var parameter = new ParameterDefinition(p.Name, p.Attributes, parameterType);
                        parameter.Constant = p.Constant;
                        foreach (var a in parameter.CustomAttributes)
                        {
                            parameter.CustomAttributes.Add(a);
                        }
                        parameter.MarshalInfo = p.MarshalInfo;
                        resolvedElementMethod.Parameters.Add(parameter);
                    }

                    foreach (var g in elementMethod.GenericParameters)
                    {
                        resolvedElementMethod.GenericParameters.Add(g);
                    }

                    elementMethod = resolvedElementMethod;
                }

                var genArgs = genMethod.GenericArguments.Select(v => ReplaceGeneric(module, v, typeParameters));
                var result = elementMethod.MakeGenericInstanceMethod(genArgs);
                return module.ImportReference(result);
            }

            return Import(module, method);
        }

        public static TypeReference Import(ModuleDefinition module, TypeReference type)
        {
            type = type.ResolveVirtualElementType();
            if (type.IsGenericParameter)
            {
                return type;
            }

            if (type.ContainsGenericParameter)
            {
                if (type is GenericInstanceType genType)
                {
                    var elementType = Import(module, genType.GetElementType());
                    var genArgs = genType.GenericArguments.Select(v => Import(module, v));
                    return elementType.MakeGenericInstanceType(genArgs);
                }

                if (type is ArrayType arrayType)
                {
                    var elementType = Import(module, arrayType.ElementType);
                    return elementType.MakeArrayType(arrayType.Rank);
                }

                if (type is PointerType pointerType)
                {
                    var elementType = Import(module, pointerType.ElementType);
                    return elementType.MakePointerType();
                }

                if (type is ByReferenceType byRefType)
                {
                    var elementType = Import(module, byRefType.ElementType);
                    return elementType.MakeByReferenceType();
                }

                if (type is PinnedType pinnedType)
                {
                    var elementType = Import(module, pinnedType.ElementType);
                    return elementType.MakePinnedType();
                }

                if (type is SentinelType sentinelType)
                {
                    var elementType = Import(module, sentinelType.ElementType);
                    return elementType.MakeSentinelType();
                }

                if (type is RequiredModifierType rmType)
                {
                    var elementType = Import(module, rmType.ElementType);
                    var modifierType = Import(module, rmType.ModifierType);
                    return elementType.MakeRequiredModifierType(modifierType);
                }

                if (type is OptionalModifierType omType)
                {
                    var elementType = Import(module, omType.ElementType);
                    var modifierType = Import(module, omType.ModifierType);
                    return elementType.MakeOptionalModifierType(modifierType);
                }

                if (type is FunctionPointerType fpType)
                {
                    var result = new FunctionPointerType();
                    result.HasThis = fpType.HasThis;
                    result.ExplicitThis = fpType.ExplicitThis;
                    result.CallingConvention = fpType.CallingConvention;
                    result.ReturnType = Import(module, fpType.ReturnType);
                    foreach (var p in fpType.Parameters)
                    {
                        var pType = Import(module, p.ParameterType);
                        var parameter = new ParameterDefinition(p.Name, p.Attributes, pType);
                        parameter.Constant = p.Constant;
                        foreach (var a in parameter.CustomAttributes)
                        {
                            parameter.CustomAttributes.Add(a);
                        }
                        parameter.MarshalInfo = p.MarshalInfo;
                        result.Parameters.Add(parameter);
                    }

                    return result;
                }
            }

            return module.ImportReference(type);
        }

        public static MethodReference Import(ModuleDefinition module, MethodReference method)
        {
            method = method.ResolveVirtualElementMethod();
            if (method.ContainsGenericParameter &&
                method is GenericInstanceMethod genMethod)
            {
                var elementMethod = genMethod.GetElementMethod();
                var genArgs = genMethod.GenericArguments.Select(v => Import(module, v));
                return elementMethod.MakeGenericInstanceMethod(genArgs);
            }

            return module.ImportReference(method);
        }

        public static SequencePoint GetSequencePoint(this MemberReference memberRef)
        {
            if (memberRef is FieldReference field)
            {
                return field.GetSequencePoint();
            }

            if (memberRef is PropertyReference property)
            {
                return property.GetSequencePoint();
            }

            if (memberRef is MethodReference method)
            {
                return method.GetSequencePoint();
            }

            if (memberRef is EventReference @event)
            {
                return @event.GetSequencePoint();
            }

            return null;
        }

        public static SequencePoint GetSequencePoint(this FieldReference fieldRef)
        {
            if (!(fieldRef is FieldDefinition field))
            {
                field = fieldRef.Resolve();
            }

            if (field == null)
            {
                return null;
            }

            var declairingType = field.DeclaringType;
            MethodDefinition constructor = null;
            OpCode stfld = default;
            if (field.IsStatic)
            {
                // 初期化子の特定方法がわからない
                //constructor = declairingType.GetStaticConstructor();
                //stfld = OpCodes.Stsfld;
            }
            else
            {
                constructor = declairingType.GetConstructors().FirstOrDefault(v => !v.IsStatic && v.Body != null && v.DebugInformation != null);
                stfld = OpCodes.Stfld;
            }

            if (constructor == null)
            {
                return null;
            }

            var debugInformation = constructor.DebugInformation;
            var instructions = constructor.Body.Instructions;
            foreach (var instruction in instructions)
            {
                if (IsCall(instruction, constructor.Name))
                {
                    break;
                }

                if (instruction.OpCode != stfld ||
                    !(instruction.Operand is FieldReference setField) ||
                    !setField.Is(field))
                {
                    continue;
                }

                var point = constructor.GetSequencePoint(instruction);
                if (point != null)
                {
                    return point;
                }
            }

            return null;
        }


        public static SequencePoint GetSequencePoint(this PropertyReference propertyRef)
        {
            if (!(propertyRef is PropertyDefinition property))
            {
                property = propertyRef.Resolve();
            }

            if (property == null)
            {
                return null;
            }

            SequencePoint point;
            if (property.GetMethod != null)
            {
                point = property.GetMethod.GetSequencePoint();
                if (point != null)
                {
                    return point;
                }
            }

            if (property.SetMethod != null)
            {
                point = property.SetMethod.GetSequencePoint();
                if (point != null)
                {
                    return point;
                }
            }

            foreach (var otherMethod in property.OtherMethods)
            {
                point = otherMethod.GetSequencePoint();
                if (point != null)
                {
                    return point;
                }
            }

            var declairingType = property.DeclaringType;
            var field = declairingType.Fields.FirstOrDefault(v => v.Name == $"<{propertyRef.Name}>k__BackingField");
            point = field.GetSequencePoint();
            return point;
        }

        public static SequencePoint GetSequencePoint(this MethodReference methodRef)
        {
            if (!(methodRef is MethodDefinition method))
            {
                method = methodRef.Resolve();
            }

            var body = method.Body;
            var debugInformation = method.DebugInformation;
            if (body == null ||
                debugInformation == null)
            {
                return null;
            }

            var it = body.Instructions.FirstOrDefault();
            while (it != null)
            {
                var point = debugInformation.GetSequencePoint(it);
                it = it.Next;
                if (point != null)
                {
                    return point;
                }
            }
            return null;
        }

        public static SequencePoint GetSequencePoint(this MethodDefinition method, Instruction instruction)
        {
            var debugInformation = method.DebugInformation;
            if (debugInformation == null)
            {
                return null;
            }

            var it = instruction;
            while (it != null)
            {
                var point = debugInformation.GetSequencePoint(it);
                it = it.Previous;
                if (point != null)
                {
                    return point;
                }
            }
            return null;
        }

        public static SequencePoint GetSequencePoint(this EventReference eventRef)
        {
            if (!(eventRef is EventDefinition @event))
            {
                @event = eventRef.Resolve();
            }

            if (@event == null)
            {
                return null;
            }

            var declairingType = @event.DeclaringType;
            var field = declairingType.Fields.FirstOrDefault(v => v.Name == eventRef.Name);
            var point = field.GetSequencePoint();
            return point;
        }

        private static bool IsCall(Instruction instruction, string name)
        {
            return instruction.OpCode == OpCodes.Call &&
                   instruction.Operand is MethodReference baseConstructor &&
                   baseConstructor.Name == name;
        }

        public static int GetHashCode_(this TypeReference self)
        {
            return TypeReferenceComparer.Default.GetHashCode(self);
        }

        public static int GetHashCode_(this MethodReference self)
        {
            return MethodReferenceComparer.Default.GetHashCode(self);
        }

        public static int GetHashCode_(this FieldReference self)
        {
            return FieldReferenceComparer.Default.GetHashCode(self);
        }

        public static int GetHashCode_(this PropertyReference self)
        {
            return PropertyReferenceComparer.Default.GetHashCode(self);
        }

        public static int GetHashCode_(this EventReference self)
        {
            return EventReferenceComparer.Default.GetHashCode(self);
        }

        public static bool Is(this TypeReference self, TypeReference cmp)
        {
            return TypeReferenceComparer.Default.Equals(self, cmp);
        }

        public static bool Is(this MethodReference self, MethodReference cmp)
        {
            return MethodReferenceComparer.Default.Equals(self, cmp);
        }

        public static bool Is(this FieldReference self, FieldReference cmp)
        {
            return FieldReferenceComparer.Default.Equals(self, cmp);
        }

        public static bool Is(this PropertyReference self, PropertyReference cmp)
        {
            return PropertyReferenceComparer.Default.Equals(self, cmp);
        }

        public static bool Is(this EventReference self, EventReference cmp)
        {
            return EventReferenceComparer.Default.Equals(self, cmp);
        }

        public static Instruction Clone(this Instruction self)
        {
            var result = Instruction.Create(OpCodes.Nop);
            result.OpCode = self.OpCode;
            result.Operand = self.Operand;
            return result;
        }

        public static FieldDefinition Clone(this FieldDefinition self)
        {
            var cloned = new FieldDefinition(self.Name, self.Attributes, self.FieldType);
            cloned.DeclaringType = self.DeclaringType;
            cloned.MetadataToken = self.MetadataToken;

            foreach (var a in self.CustomAttributes)
            {
                cloned.CustomAttributes.Add(a);
            }

            cloned.Offset = self.Offset;
            cloned.InitialValue = self.InitialValue;
            cloned.Constant = self.Constant;
            cloned.MarshalInfo = self.MarshalInfo;
            return cloned;
        }

        public static OpCode SwitchToLongJump(OpCode opCode)
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
            return opCode;
        }

        public static OpCode SwitchToShortJump(OpCode opCode)
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
            return opCode;
        }

        public static bool TryGetConstArgument(this Instruction call, string name, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(name, out var instruction))
            {
                result = null;
                return false;
            }

            return instruction.TryGetConstValue(out result);
        }

        public static bool TryGetConstArgument(this Instruction call, int argNumber, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out var instruction))
            {
                result = null;
                return false;
            }

            return instruction.TryGetConstValue(out result);
        }

        public static bool TryGetConstArgument(this Instruction call, Type type, string name, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(name, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(type, out result);
        }

        public static bool TryGetConstArgument(this Instruction call, Type type, int argNumber, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(type, out result);
        }

        public static bool TryGetConstArgument<T>(this Instruction call, string name, out T result)
        {
            if (!call.TryGetPushArgumentInstruction(name, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(out result);
        }

        public static bool TryGetConstArgument<T>(this Instruction call, int argNumber, out T result)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(out result);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, string name, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(name, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(out value);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, int argNumber, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(out value);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, Type type, string name, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(name, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(type, out value);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, Type type, int argNumber, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(type, out value);
        }

        public static bool TryGetPushConstArgumentInstruction<T>(this Instruction call, string name, out T value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(name, out instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(out value);
        }

        public static bool TryGetPushConstArgumentInstruction<T>(this Instruction call, int argNumber, out T value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(out value);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, string name, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(name, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, int argNumber, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, Type type, string name, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(name, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(type, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, Type type, int argNumber, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(type, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions<T>(this Instruction call, string name, out T value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(name, out var instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions<T>(this Instruction call, int argNumber, out T value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(argNumber, out var instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(out value, instructions);
        }

        public static bool TryGetPushArgumentInstruction(this Instruction call, string argName, out Instruction arg)
        {
            arg = null;
            if (call.OpCode != OpCodes.Call &&
                call.OpCode != OpCodes.Callvirt)
            {
                return false;
            }

            if (!(call.Operand is MethodReference method))
            {
                return false;
            }

            var methodDef = method.Resolve();
            try
            {
                var argNumber = methodDef.Parameters.Select((v, i) => (v, i)).First(v => v.v.Name == argName).i;
                return TryGetStackPushedInstruction(call, argNumber - method.Parameters.Count, out arg);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetPushArgumentInstruction(this Instruction call, int argNumber, out Instruction arg)
        {
            arg = null;
            if (call.OpCode != OpCodes.Call &&
                call.OpCode != OpCodes.Callvirt)
            {
                return false;
            }

            if (!(call.Operand is MethodReference method))
            {
                return false;
            }

            return TryGetStackPushedInstruction(call, argNumber - method.Parameters.Count, out arg);
        }

        public static bool TryGetStackPushedInstruction(this Instruction call, int targetRelativeStackCount, out Instruction result)
        {
            result = null;
            var stackCount = 0;
            var instruction = GetPrev(call);
            while (instruction != null)
            {
                var pushCount = instruction.GetPushCount();
                var beforeStackCount = stackCount;
                stackCount -= pushCount;
                if (beforeStackCount > targetRelativeStackCount && targetRelativeStackCount >= stackCount)
                {
                    result = instruction;
                    return true;
                }

                var popCount = instruction.GetPopCount();
                if (popCount == -1)
                {
                    return false;
                }
                stackCount += popCount;
                instruction = instruction.GetPrev();
            }

            return false;
        }

        public static void GetJumpTargets(this MethodBody body, HashSet<Instruction> result)
        {
            foreach (var instruction in body.Instructions)
            {
                switch (instruction.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                    case FlowControl.Cond_Branch:
                        if (instruction.Operand is Instruction target)
                        {
                            result.Add(target);
                        }
                        else if (instruction.Operand is Instruction[] targets)
                        {
                            foreach (var t in targets)
                            {
                                result.Add(t);
                            }
                        }
                        break;
                }
            }

            foreach (var h in body.ExceptionHandlers)
            {
                result.Add(h.HandlerStart);
                result.Add(h.FilterStart);
            }
        }

        public static int GetPushCount(this Instruction instruction)
        {
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            {
                var method = instruction.Operand as MethodReference;
                return method.ReturnType.FullName == "System.Void" ? 0 : 1;
            }

            return instruction.OpCode.GetPushCount();
        }

        public static int GetPopCount(this Instruction instruction)
        {
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            {
                var method = instruction.Operand as MethodReference;
                return method.Parameters.Count;
            }

            return instruction.OpCode.GetPopCount();
        }

        public static int GetPushCount(this OpCode opCode)
        {
            switch (opCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    return 0;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;
                case StackBehaviour.Push1_push1:
                    return 2;
            }
            return 0;
        }

        public static int GetPopCount(this OpCode opCode)
        {
            switch (opCode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    return 0;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                case StackBehaviour.Popref_pop1:
                    return 1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_popi:
                    return 2;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;
                case StackBehaviour.PopAll:
                    return -1;
            }

            return 0;
        }

        public static Instruction GetNext(this Instruction instruction)
        {
            var result = instruction.Next;
            while (result != null &&
                   result.OpCode == OpCodes.Nop)
            {
                result = result.Next;
            }

            return result;
        }

        public static Instruction GetPrev(this Instruction instruction)
        {
            var result = instruction.Previous;
            while (result != null &&
                   result.OpCode == OpCodes.Nop)
            {
                result = result.Previous;
            }

            return result;
        }
    }
}
