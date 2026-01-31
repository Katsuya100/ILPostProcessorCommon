using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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

        public static AssemblyDefinition LoadAssemblyDefinition(string assemblyPath, IEnumerable<string> references)
        {
            while (true)
            {
                try
                {
                    var resolver = new PostProcessorAssemblyResolver(Path.GetFileNameWithoutExtension(assemblyPath), references.Where(v => v != assemblyPath).ToArray());
                    var readerParameters = new ReaderParameters()
                    {
                        SymbolReaderProvider = new PortablePdbReaderProvider(),
                        AssemblyResolver = resolver,
                        ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                        ReadingMode = ReadingMode.Immediate,
                        ReadSymbols = true,
                    };

                    var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
                    resolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

                    return assemblyDefinition;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (BadImageFormatException)
                {
                    return null;
                }
                catch (IOException)
                {
                    continue;
                }
                catch
                {
                    LogError($"assembly load filed.{assemblyPath}");
                    throw;
                }
            }
        }

        public static string CopyAssemblySymbols(string parent, string assemblyPath)
        {
            while (true)
            {
                try
                {
                    var copiedAssemblyDirectoryPath = Path.Combine("Temp/AspectForUnity", parent);
                    if (!Directory.Exists(copiedAssemblyDirectoryPath))
                    {
                        var directoryMutex = new Mutex(false, copiedAssemblyDirectoryPath.Replace("\\", "/").Replace("/", "_"));
                        directoryMutex.WaitOne();
                        try
                        {
                            Directory.CreateDirectory(copiedAssemblyDirectoryPath);
                        }
                        finally
                        {
                            directoryMutex.ReleaseMutex();
                        }
                    }

                    var copiedAssemblyPath = Path.Combine(copiedAssemblyDirectoryPath, Path.GetFileName(assemblyPath));
                    var dllMutex = new Mutex(false, copiedAssemblyPath.Replace("\\", "/").Replace("/", "_"));
                    dllMutex.WaitOne();
                    try
                    {
                        File.Copy(assemblyPath, copiedAssemblyPath, true);
                    }
                    finally
                    {
                        dllMutex.ReleaseMutex();
                    }

                    var pdbPath = Path.ChangeExtension(assemblyPath, "pdb");
                    var copiedPdbPath = Path.ChangeExtension(copiedAssemblyPath, "pdb");
                    if (File.Exists(pdbPath))
                    {
                        var pdbMutex = new Mutex(false, copiedPdbPath.Replace("\\", "/").Replace("/", "_"));
                        try
                        {
                            pdbMutex.WaitOne();
                            File.Copy(pdbPath, copiedPdbPath, true);
                        }
                        finally
                        {
                            pdbMutex.ReleaseMutex();
                        }
                    }

                    return copiedAssemblyPath;
                }
                catch (IOException e)
                {
                    ILPPUtils.Log(e);
                    continue;
                }
            }
        }

        public static bool IsEnableGenerateIL(this ICompiledAssembly self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return !self.Defines.Contains("DISABLE_GENERATE_IL");
        }

        public static ILPostProcessResult GetResult(this ICompiledAssembly self, AssemblyDefinition assembly)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (!self.IsEnableGenerateIL())
            {
                return self.GetNullResult();
            }

            var pe  = new MemoryStream();
            var pdb = new MemoryStream();
            var writeParameter = new WriterParameters()
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream         = pdb,
                WriteSymbols         = true
            };

            assembly.Write(pe, writeParameter);
            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), Logger.Messages);
        }

        public static ILPostProcessResult GetNullResult(this ICompiledAssembly self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return new ILPostProcessResult(null, Logger.Messages);
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

        public static IEnumerable<Type> GetAllTypes(this IEnumerable<Type> self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            foreach (var type in self)
            {
                yield return type;
                foreach (var nested in type.GetNestedTypes().GetAllTypes())
                {
                    yield return nested;
                }
            }
        }

        public static IEnumerable<TypeDefinition> GetAllTypes(this IEnumerable<TypeDefinition> self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            foreach (var type in self)
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
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Where(v => v.HasAttribute(attribute));
        }

        public static IEnumerable<T> WhereHasAttribute<T>(this IEnumerable<T> self, string attribute)
            where T : Mono.Cecil.ICustomAttributeProvider
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Where(v => v.HasAttribute(attribute));
        }

        public static bool HasAttribute(this Mono.Cecil.ICustomAttributeProvider self, TypeReference attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GetAttribute(attribute) != null;
        }

        public static bool HasAttribute(this Mono.Cecil.ICustomAttributeProvider self, string attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GetAttribute(attribute) != null;
        }

        public static CustomAttribute GetAttribute(this Mono.Cecil.ICustomAttributeProvider self, TypeReference attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.CustomAttributes.FirstOrDefault(v => v.AttributeType == attribute);
        }

        public static CustomAttribute GetAttribute(this Mono.Cecil.ICustomAttributeProvider self, string attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.CustomAttributes.FirstOrDefault(v => v.AttributeType.FullName == attribute);
        }

        public static bool HasReturn(this MethodReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.ReturnType != null && self.ReturnType.FullName != "System.Void";
        }

        public static bool HasReturn(this Mono.Cecil.CallSite self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.ReturnType != null && self.ReturnType.FullName != "System.Void";
        }

        public static ParameterDefinition GetParameter(this MethodReference self, string name)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Parameters.FirstOrDefault(v => v.Name == name);
        }

        public static ParameterDefinition GetParameterWithAttribute(this MethodReference self, string attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Parameters.FirstOrDefault(v => v.HasAttribute(attribute));
        }

        public static GenericParameter GetGenericParameter(this MethodReference self, string name)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GenericParameters.FirstOrDefault(v => v.Name == name);
        }

        public static GenericParameter GetGenericParameterWithAttribute(this MethodReference self, string attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GenericParameters.FirstOrDefault(v => v.HasAttribute(attribute));
        }

        public static GenericParameter GetGenericParameter(this TypeReference self, string name)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GenericParameters.FirstOrDefault(v => v.Name == name);
        }

        public static GenericParameter GetGenericParameterWithAttribute(this TypeReference self, string attribute)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GenericParameters.FirstOrDefault(v => v.HasAttribute(attribute));
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

        public static Instruction LoadElement(TypeReference elementType)
        {
            var typeDef = elementType.Resolve();
            if (typeDef.IsEnum)
            {
                elementType = typeDef.GetEnumUnderlyingType();
            }

            var name = elementType.FullName;
            if (elementType.IsPointer ||
                name == "System.IntPtr" ||
                name == "System.UIntPtr")
            {
                return Instruction.Create(OpCodes.Ldelem_I);
            }

            if (name == "System.Boolean" ||
                name == "System.SByte" ||
                name == "System.Byte")
            {
                return Instruction.Create(OpCodes.Ldelem_I1);
            }

            if (name == "System.Int16" ||
                name == "System.UInt16" ||
                name == "System.Char")
            {
                return Instruction.Create(OpCodes.Ldelem_I2);
            }

            if (name == "System.Int32" ||
                name == "System.UInt32")
            {
                return Instruction.Create(OpCodes.Ldelem_I4);
            }

            if (name == "System.Int64" ||
                name == "System.UInt64")
            {
                return Instruction.Create(OpCodes.Ldelem_I8);
            }

            if (name == "System.Single")
            {
                return Instruction.Create(OpCodes.Ldelem_R4);
            }

            if (name == "System.Double")
            {
                return Instruction.Create(OpCodes.Ldelem_R8);
            }

            if (!typeDef.IsValueType)
            {
                return Instruction.Create(OpCodes.Ldelem_Ref);
            }

            return Instruction.Create(OpCodes.Ldelem_Any, elementType);
        }

        public static Instruction LoadElementAddress(TypeReference elementType)
        {
            return Instruction.Create(OpCodes.Ldelema, elementType);
        }

        public static Instruction SetElement(TypeReference elementType)
        {
            var typeDef = elementType.Resolve();
            if (typeDef.IsEnum)
            {
                elementType = typeDef.GetEnumUnderlyingType();
            }

            var name = elementType.FullName;
            if (elementType.IsPointer ||
                name == "System.IntPtr" ||
                name == "System.UIntPtr")
            {
                return Instruction.Create(OpCodes.Stelem_I);
            }

            if (name == "System.Boolean" ||
                name == "System.SByte" ||
                name == "System.Byte")
            {
                return Instruction.Create(OpCodes.Stelem_I1);
            }

            if (name == "System.Int16" ||
                name == "System.UInt16" ||
                name == "System.Char")
            {
                return Instruction.Create(OpCodes.Stelem_I2);
            }

            if (name == "System.Int32" ||
                name == "System.UInt32")
            {
                return Instruction.Create(OpCodes.Stelem_I4);
            }

            if (name == "System.Int64" ||
                name == "System.UInt64")
            {
                return Instruction.Create(OpCodes.Stelem_I8);
            }

            if (name == "System.Single")
            {
                return Instruction.Create(OpCodes.Stelem_R4);
            }

            if (name == "System.Double")
            {
                return Instruction.Create(OpCodes.Stelem_R8);
            }

            if (!typeDef.IsValueType)
            {
                return Instruction.Create(OpCodes.Stelem_Ref);
            }

            return Instruction.Create(OpCodes.Stelem_Any, elementType);
        }

        public static Instruction LoadArgument(ParameterDefinition parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            var number = parameter.Index;
            if (parameter.Method.HasThis)
            {
                ++number;
            }

            switch (number)
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

            if (number < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Ldarg_S, parameter);
            }

            return Instruction.Create(OpCodes.Ldarg, parameter);
        }

        public static Instruction LoadArgumentAddress(ParameterDefinition parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            var number = parameter.Index;
            if (parameter.Method.HasThis)
            {
                ++number;
            }

            if (parameter.Index < byte.MaxValue)
            {
                return Instruction.Create(OpCodes.Ldarga_S, parameter);
            }

            return Instruction.Create(OpCodes.Ldarga, parameter);
        }

        public static Instruction LoadIndirect(TypeReference typeRef)
        {
            if (typeRef.IsGenericParameter)
            {
                return Instruction.Create(OpCodes.Ldobj, typeRef);
            }

            switch (typeRef.MetadataType)
            {
                case MetadataType.FunctionPointer:
                case MetadataType.Pointer:
                case MetadataType.IntPtr:
                case MetadataType.UIntPtr:
                case MetadataType.Pinned:
                    return Instruction.Create(OpCodes.Ldind_I);
                case MetadataType.SByte:
                    return Instruction.Create(OpCodes.Ldind_I1);
                case MetadataType.Boolean:
                case MetadataType.Byte:
                    return Instruction.Create(OpCodes.Ldind_U1);
                case MetadataType.Int16:
                    return Instruction.Create(OpCodes.Ldind_I2);
                case MetadataType.UInt16:
                case MetadataType.Char:
                    return Instruction.Create(OpCodes.Ldind_U2);
                case MetadataType.Int32:
                    return Instruction.Create(OpCodes.Ldind_I4);
                case MetadataType.UInt32:
                    return Instruction.Create(OpCodes.Ldind_U4);
                case MetadataType.Int64:
                case MetadataType.UInt64:
                    return Instruction.Create(OpCodes.Ldind_I8);
                case MetadataType.Single:
                    return Instruction.Create(OpCodes.Ldind_R4);
                case MetadataType.Double:
                    return Instruction.Create(OpCodes.Ldind_R8);
                case MetadataType.Object:
                case MetadataType.String:
                case MetadataType.Class:
                case MetadataType.Array:
                    return Instruction.Create(OpCodes.Ldind_Ref);
                case MetadataType.ValueType:
                    return Instruction.Create(OpCodes.Ldobj, typeRef);
                default:
                    var type = typeRef.Resolve();
                    if (type == null)
                    {
                        return Instruction.Create(OpCodes.Ldobj, typeRef);
                    }

                    if (type.IsValueType)
                    {
                        return Instruction.Create(OpCodes.Ldobj, type);
                    }

                    return Instruction.Create(OpCodes.Ldind_Ref);
            }
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


        public static int GetLoadArgumentIndex(Instruction instruction)
        {
            var loadArg = instruction.OpCode;
            if (loadArg == OpCodes.Ldarg_0)
            {
                return 0;
            }

            if (loadArg == OpCodes.Ldarg_1)
            {
                return 1;
            }

            if (loadArg == OpCodes.Ldarg_2)
            {
                return 2;
            }

            if (loadArg == OpCodes.Ldarg_3)
            {
                return 3;
            }

            if (loadArg == OpCodes.Ldarg_S ||
                loadArg == OpCodes.Ldarga_S ||
                loadArg == OpCodes.Ldarg ||
                loadArg == OpCodes.Ldarga)
            {
                var parameter = (ParameterDefinition)instruction.Operand;
                return parameter.Index;
            }

            return -1;
        }

        public static int GetSetArgumentIndex(Instruction instruction)
        {
            var setArg = instruction.OpCode;
            if (setArg == OpCodes.Starg_S ||
                setArg == OpCodes.Starg)
            {
                var parameter = (ParameterDefinition)instruction.Operand;
                return parameter.Index;
            }

            return -1;
        }

        public static int GetLoadLocalIndex(Instruction instruction)
        {
            var loadLocal = instruction.OpCode;
            if (loadLocal == OpCodes.Ldloc_0)
            {
                return 0;
            }

            if (loadLocal == OpCodes.Ldloc_1)
            {
                return 1;
            }

            if (loadLocal == OpCodes.Ldloc_2)
            {
                return 2;
            }

            if (loadLocal == OpCodes.Ldloc_3)
            {
                return 3;
            }

            if (loadLocal == OpCodes.Ldloc_S ||
                loadLocal == OpCodes.Ldloca_S ||
                loadLocal == OpCodes.Ldloc ||
                loadLocal == OpCodes.Ldloca)
            {
                var variable = (VariableDefinition)instruction.Operand;
                return variable.Index;
            }

            return -1;
        }

        public static int GetSetLocalIndex(Instruction instruction)
        {
            var setLocal = instruction.OpCode;
            if (setLocal == OpCodes.Stloc_0)
            {
                return 0;
            }

            if (setLocal == OpCodes.Stloc_1)
            {
                return 1;
            }

            if (setLocal == OpCodes.Stloc_2)
            {
                return 2;
            }

            if (setLocal == OpCodes.Stloc_3)
            {
                return 3;
            }

            if (setLocal == OpCodes.Stloc_S ||
                setLocal == OpCodes.Stloc)
            {
                var variable = (VariableDefinition)instruction.Operand;
                return variable.Index;
            }

            return -1;
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

        public static bool TryGetConstValue<T>(this Instruction self, MethodReference method, out T value, List<Instruction> instructions = null)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            if (self.TryGetConstValue(method, typeof(T), out object r, instructions) &&
                r is T resultValue)
            {
                value = resultValue;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetConstValue(this Instruction self, MethodReference method, Type type, out object value, List<Instruction> instructions = null)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            if (!self.TryGetConstValue(method, out value, instructions))
            {
                return false;
            }

            return TryCast(type, value, out value);
        }

        public static bool TryGetConstValue(this Instruction self, MethodReference methodRef, out object value, List<Instruction> instructions = null)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var opCode = self.OpCode;
            var operand = self.Operand;
            if (opCode == OpCodes.Ldnull)
            {
                instructions?.Add(self);
                value = null;
                return true;
            }

            if (opCode == OpCodes.Ldstr)
            {
                instructions?.Add(self);
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
                instructions?.Add(self);
                value = 0;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_1)
            {
                instructions?.Add(self);
                value = 1;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_2)
            {
                instructions?.Add(self);
                value = 2;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_3)
            {
                instructions?.Add(self);
                value = 3;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_4)
            {
                instructions?.Add(self);
                value = 4;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_5)
            {
                instructions?.Add(self);
                value = 5;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_6)
            {
                instructions?.Add(self);
                value = 6;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_7)
            {
                instructions?.Add(self);
                value = 7;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_8)
            {
                instructions?.Add(self);
                value = 8;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_M1)
            {
                instructions?.Add(self);
                value = -1;
                return true;
            }
            if (opCode == OpCodes.Ldc_I4_S || opCode == OpCodes.Ldc_I4)
            {
                instructions?.Add(self);
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
                instructions?.Add(self);
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
                instructions?.Add(self);
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
                instructions?.Add(self);
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
                instructions?.Add(self);
                value = self.Operand;
                return true;
            }

            if (opCode == OpCodes.Ldftn)
            {
                if (operand is MethodReference method &&
                    method.IsStatic())
                {
                    instructions?.Add(self);
                    value = operand;
                    return true;
                }

                value = null;
                return false;
            }

            if (opCode == OpCodes.Conv_I1)
            {
                if (!self.TryGetStackPushedInstruction(methodRef, -1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(methodRef, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
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
                if (!self.TryGetStackPushedInstruction(methodRef, -1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(methodRef, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
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
                if (!self.TryGetStackPushedInstruction(methodRef, -1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(methodRef, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
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
                if (!self.TryGetStackPushedInstruction(methodRef, -1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(methodRef, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
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


            if (opCode == OpCodes.Newobj)
            {
                var method = self.Operand as MethodReference;
                if (!method.DeclaringType.IsDelegate() ||
                    method.Parameters.Count != 2 ||
                    method.Parameters[0].ParameterType.FullName != "System.Object" ||
                    method.Parameters[1].ParameterType.FullName != "System.IntPtr")
                {
                    value = null;
                    return false;
                }

                if (!self.TryGetPushConstArgumentInstructions(methodRef, 0, out value, instructions))
                {
                    return false;
                }

                if (!self.TryGetPushConstArgumentInstructions(methodRef, 1, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
                return true;
            }

            if (opCode == OpCodes.Box)
            {
                if (!self.TryGetStackPushedInstruction(methodRef, -1, out var prev))
                {
                    value = null;
                    return false;
                }

                if (!prev.TryGetConstValue(methodRef, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
                return true;
            }

            if (opCode == OpCodes.Call)
            {
                var method = self.Operand as MethodReference;
                if (method.FullName != "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)")
                {
                    value = null;
                    return false;
                }

                if (!self.TryGetPushConstArgumentInstructions(methodRef, 0, out value, instructions))
                {
                    return false;
                }

                instructions?.Add(self);
                return true;
            }

            if (opCode == OpCodes.Ldsfld)
            {
                var field = self.Operand as FieldReference;
                var declaringTypeName = field.DeclaringType.Name;
                if (declaringTypeName != "$$ConstTable" && !declaringTypeName.StartsWith("$$StaticTable_", StringComparison.Ordinal))
                {
                    value = null;
                    return false;
                }

                value = field;
                instructions?.Add(self);
                return true;
            }

            value = null;
            return false;
        }


        public static IEnumerable<System.Reflection.ConstructorInfo> FindConstructs<T>(System.Reflection.Assembly assembly)
            where T : Attribute
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetConstructors())
                {
                    if (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<T>(method) == null)
                    {
                        continue;
                    }

                    yield return method;
                }
            }
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

        public static bool IsStatic(this MemberReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            {
                if (self is FieldDefinition field)
                {
                    return field.IsStatic;
                }

                if (self is PropertyDefinition property)
                {
                    return (property.GetMethod?.IsStatic ?? false) ||
                           (property.SetMethod?.IsStatic ?? false) ||
                           (property.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }

                if (self is MethodDefinition method)
                {
                    return method.IsStatic;
                }

                if (self is EventDefinition @event)
                {
                    return (@event.AddMethod?.IsStatic ?? false) ||
                           (@event.RemoveMethod?.IsStatic ?? false) ||
                           (@event.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }
            }
            {
                if (self is FieldReference field)
                {
                    return field.Resolve().IsStatic;
                }

                if (self is PropertyReference property)
                {
                    var p = property.Resolve();
                    return (p.GetMethod?.IsStatic ?? false) ||
                           (p.SetMethod?.IsStatic ?? false) ||
                           (p.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }

                if (self is MethodReference method)
                {
                    return method.Resolve().IsStatic;
                }

                if (self is EventDefinition @event)
                {
                    var e = @event.Resolve();
                    return (e.AddMethod?.IsStatic ?? false) ||
                           (e.RemoveMethod?.IsStatic ?? false) ||
                           (e.OtherMethods?.Any(v => v.IsStatic) ?? false);
                }
            }

            return false;
        }

        public static bool IsPublic(this MemberReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            {
                if (self is TypeDefinition type)
                {
                    return type.IsPublic || type.IsNestedPublic;
                }

                if (self is FieldDefinition field)
                {
                    return field.IsPublic;
                }

                if (self is PropertyDefinition property)
                {

                    return (property.GetMethod?.IsPublic ?? false) ||
                           (property.SetMethod?.IsPublic ?? false) ||
                           (property.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }

                if (self is MethodDefinition method)
                {
                    return method.IsPublic;
                }

                if (self is EventDefinition @event)
                {
                    return (@event.AddMethod?.IsPublic ?? false) ||
                           (@event.RemoveMethod?.IsPublic ?? false) ||
                           (@event.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }
            }
            {
                if (self is TypeReference type)
                {
                    var typeRef = type.Resolve();
                    return typeRef.IsPublic || typeRef.IsNestedPublic;
                }

                if (self is FieldReference field)
                {
                    return field.Resolve().IsPublic;
                }

                if (self is PropertyReference property)
                {
                    var p = property.Resolve();
                    return (p.GetMethod?.IsPublic ?? false) ||
                           (p.SetMethod?.IsPublic ?? false) ||
                           (p.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }

                if (self is MethodReference method)
                {
                    return method.Resolve().IsPublic;
                }

                if (self is EventDefinition @event)
                {
                    var e = @event.Resolve();
                    return (e.AddMethod?.IsPublic ?? false) ||
                           (e.RemoveMethod?.IsPublic ?? false) ||
                           (e.OtherMethods?.Any(v => v.IsPublic) ?? false);
                }
            }

            return false;
        }

        public static bool IsDelegate(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            var typeDef = self.Resolve();
            if (typeDef == null)
            {
                return false;
            }

            var baseType = typeDef.BaseType?.FullName ?? string.Empty;
            return baseType == "System.MulticastDelegate" ||
                   baseType == "System.Delegate";
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

        public static bool IsStructRecursive(this Type self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (self.IsPrimitive || self.IsEnum)
            {
                return true;
            }

            if (!self.IsValueType)
            {
                return false;
            }

            var fields = self.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fields.Select(v => v.FieldType).All(IsStructRecursive);
        }

        public static bool IsStructRecursive(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

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

        public static MethodReference MakeGenericInstanceMethod(this MethodReference self, IEnumerable<TypeReference> arguments)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            var genericInstanceMethod = new GenericInstanceMethod(self);
            foreach (TypeReference item in arguments)
            {
                genericInstanceMethod.GenericArguments.Add(item);
            }
            return genericInstanceMethod;
        }

        public static GenericInstanceType MakeGenericInstanceType(this TypeReference self, IEnumerable<TypeReference> arguments)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            GenericInstanceType genericInstanceType = new GenericInstanceType(self);
            foreach (TypeReference item in arguments)
            {
                genericInstanceType.GenericArguments.Add(item);
            }

            return genericInstanceType;
        }

        public static IList<TypeReference> GetGenericArguments(this MethodReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (!(self is GenericInstanceMethod genMethod))
            {
                return Array.Empty<TypeReference>();
            }

            return genMethod.GenericArguments;
        }

        public static IList<TypeReference> GetGenericArguments(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (!(self is GenericInstanceType genType))
            {
                return Array.Empty<TypeReference>();
            }

            return genType.GenericArguments;
        }

        public static IEnumerable<TypeReference> GetNestedTypes(this TypeReference self, TypeDefinition type)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (!(self is GenericInstanceType genType))
            {
                return type.NestedTypes;
            }

            var genArgs = genType.GenericArguments;
            return type.NestedTypes.Where(v => v.GenericParameters.Count == genArgs.Count)
                                      .Select(v2 => v2.GetElementType().MakeGenericInstanceType(genArgs))
                                      .OfType<TypeReference>();
        }

        public static TypeReference GetDeclaringType(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (self.DeclaringType == null)
            {
                return null;
            }

            if (self.IsGenericDefinition())
            {
                var def = self.DeclaringType.Resolve();
                if (def == null)
                {
                    return self.DeclaringType.GetElementType();
                }
                return def;
            }

            if (self is GenericInstanceType genType)
            {
                TypeReference type = self.Resolve();
                if (type == null)
                {
                    type = self.GetElementType();
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
                    return self.DeclaringType;
                }

                return declaringType.MakeGenericInstanceType(declairingGenArgs);
            }

            return self.DeclaringType;
        }

        public static void GetBaseTypeAndInterfaces(this TypeReference self, List<TypeReference> results, bool onlyInterface = false)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null || type.BaseType == null)
            {
                return;
            }

            if (self is GenericInstanceType genType)
            {
                GetBaseTypeAndInterfaces(genType, onlyInterface, results);
            }
            else if (self is ArrayType arrayType)
            {
                GetBaseTypeAndInterfaces(arrayType, onlyInterface, results);
            }
            else if (self is ByReferenceType byRefType)
            {
                GetBaseTypeAndInterfaces(byRefType, onlyInterface, results);
            }
            else if (self is PointerType pointerType)
            {
                GetBaseTypeAndInterfaces(pointerType, onlyInterface, results);
            }
            else if (self is FunctionPointerType funcPtrType)
            {
                GetBaseTypeAndInterfaces(funcPtrType, onlyInterface, results);
            }
            else if (self is RequiredModifierType reqmodType)
            {
                GetBaseTypeAndInterfaces(reqmodType, onlyInterface, results);
            }
            else if (self is OptionalModifierType optmodType)
            {
                GetBaseTypeAndInterfaces(optmodType, onlyInterface, results);
            }
            else if (self is SentinelType sentinelType)
            {
                GetBaseTypeAndInterfaces(sentinelType, onlyInterface, results);
            }
            else if (self is PinnedType pinnedType)
            {
                GetBaseTypeAndInterfaces(pinnedType, onlyInterface, results);
            }
            else
            {
                if (type.BaseType != null && !onlyInterface)
                {
                    results.Add(type.BaseType);
                }

                foreach (var interfaceImpl in type.Interfaces)
                {
                    results.Add(interfaceImpl.InterfaceType);
                }
            }
        }

        public static void GetBaseTypeAndInterfaces(this GenericInstanceType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null)
            {
                return;
            }

            using (ThreadStaticListPool.Get<TypeReference>(out var baseTypes))
            {
                if (type.BaseType != null && !onlyInterface)
                {
                    baseTypes.Add(type.BaseType);
                }

                foreach (var interfaceImpl in type.Interfaces)
                {
                    baseTypes.Add(interfaceImpl.InterfaceType);
                }

                foreach (var baseType in baseTypes)
                {
                    if (baseType.ContainsGenericParameter)
                    {
                        if (baseType is GenericInstanceType genericInstanceType)
                        {
                            bool isReplaced = false;
                            using (ThreadStaticListPool.Get<TypeReference>(out var genArgs))
                            {
                                for (int i = 0; i < genericInstanceType.GenericArguments.Count; ++i)
                                {
                                    var arg = genericInstanceType.GenericArguments[i];
                                    if (self.TryReplaceGenericParameter(arg, out arg))
                                    {
                                        isReplaced = true;
                                    }
                                    genArgs.Add(arg);
                                }

                                if (!isReplaced)
                                {
                                    results.Add(baseType);
                                    return;
                                }

                                results.Add(genericInstanceType.Resolve().MakeGenericInstanceType(genArgs));
                            }
                        }
                        else if (baseType is RequiredModifierType reqmodType)
                        {
                            bool isReplaced = false;
                            if (self.TryReplaceGenericParameter(reqmodType.ElementType, out var elementType))
                            {
                                isReplaced = true;
                            }
                            if (self.TryReplaceGenericParameter(reqmodType.ModifierType, out var modifierType))
                            {
                                isReplaced = true;
                            }
                            if (!isReplaced)
                            {
                                results.Add(baseType);
                                return;
                            }
                            results.Add(elementType.MakeRequiredModifierType(modifierType));
                        }
                        else if (baseType is OptionalModifierType optmodType)
                        {
                            bool isReplaced = false;
                            if (self.TryReplaceGenericParameter(optmodType.ElementType, out var elementType))
                            {
                                isReplaced = true;
                            }
                            if (self.TryReplaceGenericParameter(optmodType.ModifierType, out var modifierType))
                            {
                                isReplaced = true;
                            }
                            if (!isReplaced)
                            {
                                results.Add(baseType);
                                return;
                            }
                            results.Add(elementType.MakeOptionalModifierType(modifierType));
                        }
                        else
                        {
                            ILPPUtils.Log($"invalid base type.{baseType.FullName}");
                            results.Add(baseType);
                        }
                    }
                    else
                    {
                        results.Add(baseType);
                    }
                }
            }
        }

        public static void GetBaseTypeAndInterfaces(this ArrayType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null)
            {
                return;
            }

            results.Add(type.Module.ImportReference(typeof(Array)));
            results.Add(type.Module.ImportReference(typeof(IStructuralComparable)));
            results.Add(type.Module.ImportReference(typeof(IStructuralEquatable)));
            results.Add(type.Module.ImportReference(typeof(IReadOnlyList<>)).MakeGenericInstanceType(self.ElementType));
            results.Add(type.Module.ImportReference(typeof(ICloneable)));
            results.Add(type.Module.ImportReference(typeof(ICollection)));
            results.Add(type.Module.ImportReference(typeof(IEnumerable<>)).MakeGenericInstanceType(self.ElementType));
            results.Add(type.Module.ImportReference(typeof(IEnumerable)));
            results.Add(type.Module.ImportReference(typeof(IList<>)).MakeGenericInstanceType(self.ElementType));
            results.Add(type.Module.ImportReference(typeof(IList)));
            results.Add(type.Module.ImportReference(typeof(IReadOnlyCollection<>)).MakeGenericInstanceType(self.ElementType));
            results.Add(type.Module.ImportReference(typeof(ICollection<>)).MakeGenericInstanceType(self.ElementType));
        }

        public static void GetBaseTypeAndInterfaces(this ByReferenceType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            GetBaseTypeAndInterfaces(self.ElementType, results, true);
        }

        public static void GetBaseTypeAndInterfaces(this PointerType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            // ignore
        }

        public static void GetBaseTypeAndInterfaces(this FunctionPointerType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            // ignore
        }

        public static void GetBaseTypeAndInterfaces(this RequiredModifierType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            GetBaseTypeAndInterfaces(self.ElementType, results, true);
        }

        public static void GetBaseTypeAndInterfaces(this OptionalModifierType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            GetBaseTypeAndInterfaces(self.ElementType, results, true);
        }

        public static void GetBaseTypeAndInterfaces(this SentinelType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            // ignore
        }

        public static void GetBaseTypeAndInterfaces(this PinnedType self, bool onlyInterface, List<TypeReference> results)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            GetBaseTypeAndInterfaces(self.ElementType, results, true);
        }

        public static IEnumerable<FieldDefinition> GetFields(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

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
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
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
            if (src is GenericParameter genericParameter)
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

            if (src.ContainsGenericParameter)
            {
                if (src is GenericInstanceType genType)
                {
                    bool isReplaced = false;
                    using (ThreadStaticArrayPool.Get<TypeReference>(out var genArgs, genType.GenericArguments.Count))
                    {
                        for (int i = 0; i < genArgs.Length; ++i)
                        {
                            if (TryReplaceGenericParameter(typeRef, genType.GenericArguments[i], out genArgs[i]))
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
                else if (src is ArrayType arrayType)
                {
                    bool isReplaced = false;
                    if (TryReplaceGenericParameter(typeRef, arrayType.ElementType, out var elementType))
                    {
                        isReplaced = true;
                    }

                    if (!isReplaced)
                    {
                        return false;
                    }

                    if (arrayType.IsVector)
                    {
                        result = elementType.MakeArrayType();
                        return true;
                    }

                    result = elementType.MakeArrayType(arrayType.Rank);
                    return true;
                }
                else if (src is ByReferenceType byRefType)
                {
                    if (TryReplaceGenericParameter(typeRef, byRefType.ElementType, out var elementType))
                    {
                        result = elementType.MakeByReferenceType();
                        return true;
                    }
                }
                else if (src is PointerType pointerType)
                {
                    if (TryReplaceGenericParameter(typeRef, pointerType.ElementType, out var elementType))
                    {
                        result = elementType.MakePointerType();
                        return true;
                    }
                }
                else if (src is FunctionPointerType funcPtrType)
                {
                    bool isReplaced = false;
                    if (TryReplaceGenericParameter(typeRef, funcPtrType.ReturnType, out var returnType))
                    {
                        isReplaced = true;
                    }

                    using (ThreadStaticArrayPool.Get<ParameterDefinition>(out var parameters, funcPtrType.Parameters.Count))
                    {
                        for (int i = 0; i < parameters.Length; ++i)
                        {
                            var param = funcPtrType.Parameters[i];
                            if (TryReplaceGenericParameter(typeRef, param.ParameterType, out var paramType))
                            {
                                isReplaced = true;
                                var newParam = new ParameterDefinition(param.Name, param.Attributes, paramType);
                                foreach (var attr in param.CustomAttributes)
                                {
                                    newParam.CustomAttributes.Add(attr);
                                }
                                newParam.Constant = param.Constant;
                                newParam.MarshalInfo = param.MarshalInfo;
                                parameters[i] = newParam;
                            }
                            else
                            {
                                parameters[i] = param;
                            }
                        }
                        if (isReplaced)
                        {
                            var newFuncPtr = new FunctionPointerType();
                            newFuncPtr.ReturnType = returnType;
                            foreach (var param in parameters)
                            {
                                newFuncPtr.Parameters.Add(param);
                            }
                            result = newFuncPtr;
                            return true;
                        }
                    }
                }
                else if (src is RequiredModifierType reqModType)
                {
                    bool isReplaced = false;
                    if (TryReplaceGenericParameter(typeRef, reqModType.ElementType, out var elementType))
                    {
                        isReplaced = true;
                    }
                    if (TryReplaceGenericParameter(typeRef, reqModType.ModifierType, out var modifierType))
                    {
                        isReplaced = true;
                    }

                    if (!isReplaced)
                    {
                        return false;
                    }

                    result = elementType.MakeRequiredModifierType(modifierType);
                    return true;
                }
                else if (src is OptionalModifierType optModType)
                {
                    bool isReplaced = false;
                    if (TryReplaceGenericParameter(typeRef, optModType.ElementType, out var elementType))
                    {
                        isReplaced = true;
                    }
                    if (TryReplaceGenericParameter(typeRef, optModType.ModifierType, out var modifierType))
                    {
                        isReplaced = true;
                    }
                    if (!isReplaced)
                    {
                        return false;
                    }
                    result = elementType.MakeOptionalModifierType(modifierType);
                    return true;
                }
                else if (src is SentinelType sentinelType)
                {
                    if (TryReplaceGenericParameter(typeRef, sentinelType.ElementType, out var elementType))
                    {
                        result = elementType.MakeSentinelType();
                        return true;
                    }
                }
                else if (src is PinnedType pinnedType)
                {
                    if (TryReplaceGenericParameter(typeRef, pinnedType.ElementType, out var elementType))
                    {
                        result = elementType.MakePinnedType();
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsVolatile(this System.Reflection.FieldInfo self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GetRequiredCustomModifiers().Contains(typeof(IsVolatile));
        }

        public static bool IsVolatile(this FieldReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return (self.FieldType is RequiredModifierType modType &&
                    modType.ModifierType.FullName == "System.Runtime.CompilerServices.IsVolatile");
        }

        public static TypeReference GetForceInstancedGenericType(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
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
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.HasGenericParameters && !self.IsGenericInstance;
        }

        public static bool IsGenericDefinition(this MethodReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.HasGenericParameters && !self.IsGenericInstance;
        }

        public static bool IsEnum(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null)
            {
                return false;
            }

            return type.IsEnum;
        }

        public static bool IsUnmanaged(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            if (self.IsPrimitive || self.IsPointer || self.IsFunctionPointer)
            {
                return true;
            }

            var type = self.Resolve();
            if (type == null ||
                !type.IsValueType)
            {
                return false;
            }

            if (type.IsEnum || type.IsPrimitive)
            {
                return true;
            }

            IEnumerable<FieldDefinition> fields = self is GenericInstanceType genType ? GetFields(genType) : type.Fields;
            return fields.Where(v => !v.IsStatic).All(v => IsCompatibleUnmanagedConstraint(v.FieldType));
        }

        public static bool IsString(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var result = self.FullName == "System.String";
            return result;
        }

        public static bool IsNullable(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null)
            {
                return false;
            }

            return type.FullName == "System.Nullable`1";
        }

        public static bool IsInterface(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null)
            {
                return false;
            }
            return type.IsInterface;
        }

        public static bool IsClass(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null ||
                type.IsInterface ||
                type.IsValueType)
            {
                return false;
            }

            return true;
        }

        public static bool IsStruct(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            if (self.IsPrimitive)
            {
                return true;
            }

            if (self.IsGenericParameter)
            {
                return false;
            }

            var typeDef = self.Resolve();
            if (typeDef == null ||
                typeDef.IsEnum ||
                typeDef.IsValueType)
            {
                return true;
            }

            return false;
        }

        public static bool IsStaticType(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null)
            {
                return false;
            }

            return type.IsSealed && type.IsAbstract;
        }

        public static bool IsSealed(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var typeDef = self.Resolve();
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

        public static bool HasDefaultConstructor(this TypeReference self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var type = self.Resolve();
            if (type == null ||
                type.IsAbstract ||
                type.IsInterface)
            {
                return false;
            }

            if (type.IsValueType)
            {
                return true;
            }

            var result = type.Methods.Any(m => m.IsConstructor && m.IsPublic && !m.HasParameters);
            return result;
        }

        public static bool HasNewConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.HasDefaultConstructorConstraint &&
                   !self.HasStructConstraint() &&
                   !self.HasUnmanagedConstraint();
        }

        public static bool HasClassConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            return self.HasReferenceTypeConstraint &&
                   self.GetNullableContextStatus() != NullableStatus.Nullable &&
                   self.Constraints.All(v => v.GetNullableStatus() != NullableStatus.Nullable);
        }

        public static bool HasClassNullableConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.HasReferenceTypeConstraint &&
                   (self.GetNullableContextStatus() == NullableStatus.Nullable ||
                   self.Constraints.Any(v => v.GetNullableStatus() == NullableStatus.Nullable));
        }

        public static bool HasUnmanagedConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GetAttribute("System.Runtime.CompilerServices.IsUnmanagedAttribute") != null ||
                   self.Constraints.Any(v => v.HasUnmanagedConstraint());
        }

        public static bool HasUnmanagedConstraint(this GenericParameterConstraint self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (self.ConstraintType is not RequiredModifierType modReq)
            {
                return false;
            }

            return modReq.ModifierType.FullName == typeof(UnmanagedType).FullName;
        }

        public static bool HasNotNullConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return !self.HasReferenceTypeConstraint &&
                   self.GetNullableContextStatus() == NullableStatus.NotNull;
        }

        public static bool HasStructConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Constraints.Any(HasStructConstraint);
        }

        public static bool HasStructConstraint(this GenericParameterConstraint self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.ConstraintType.FullName == typeof(ValueType).FullName;
        }

        public static bool HasBaseTypeConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GetBaseTypeConstraints().Any();
        }

        public static IEnumerable<GenericParameterConstraint> GetBaseTypeConstraints(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Constraints.Where(v => !v.HasStructConstraint() && !v.HasUnmanagedConstraint() && v.GetNullableStatus() != NullableStatus.Nullable);
        }

        public static bool HasBaseTypeNullableConstraint(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.GetBaseTypeNullableConstraints().Any();
        }

        public static IEnumerable<GenericParameterConstraint> GetBaseTypeNullableConstraints(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.Constraints.Where(v => !v.HasStructConstraint() && !v.HasUnmanagedConstraint() && v.GetNullableStatus() == NullableStatus.Nullable);
        }

        public static NullableStatus GetNullableContextStatus(this GenericParameter self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            if (self.Owner is not Mono.Cecil.ICustomAttributeProvider owner)
            {
                return NullableStatus.None;
            }

            var attr = owner.GetAttribute("System.Runtime.CompilerServices.NullableContextAttribute");
            if (attr == null || !attr.ConstructorArguments.Any())
            {
                return NullableStatus.None;
            }

            var arg = attr.ConstructorArguments[0].Value;
            if (arg is byte b)
            {
                return (NullableStatus)b;
            }

            return NullableStatus.None;
        }

        public static NullableStatus GetNullableStatus(this GenericParameterConstraint self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            var attr = self.GetAttribute("System.Runtime.CompilerServices.NullableAttribute");
            if (attr == null || !attr.ConstructorArguments.Any())
            {
                return NullableStatus.None;
            }

            var arg = attr.ConstructorArguments[0].Value;
            if (arg is byte b)
            {
                return (NullableStatus)b;
            }

            return NullableStatus.None;
        }

        public static bool IsBoxingRequired(this TypeReference srcRef, TypeReference dstRef)
        {
            if (srcRef == null)
            {
                throw new ArgumentNullException(nameof(srcRef));
            }
            if (dstRef == null)
            {
                throw new ArgumentNullException(nameof(dstRef));
            }

            if (srcRef.IsByReference || srcRef.IsPointer || srcRef.IsFunctionPointer)
            {
                return false;
            }

            if (srcRef.IsGenericParameter)
            {
                if (dstRef.IsGenericParameter)
                {
                    return false;
                }

                if (dstRef.FullName == typeof(object).FullName ||
                    dstRef.FullName == typeof(ValueType).FullName)
                {
                    return true;
                }

                var dst = dstRef.Resolve();
                if (dst == null)
                {
                    Log($"unable to resolve type reference: {dstRef.FullName}");
                    return true;
                }

                if (dst.IsInterface)
                {
                    return true;
                }
            }

            bool srcIsValueType = false;
            if (srcRef.IsArray)
            {
                srcIsValueType = false;
            }
            else
            {
                srcIsValueType = srcRef.Resolve().IsValueType;
            }

            bool dstIsValueType = false;
            if (dstRef.IsArray)
            {
                dstIsValueType = false;
            }
            else if (dstRef.IsGenericParameter)
            {
                dstIsValueType = srcIsValueType;
            }
            else
            {
                dstIsValueType = dstRef.Resolve().IsValueType;
            }

            return srcIsValueType && !dstIsValueType;
        }

        public static bool IsCompatible(this TypeReference src, TypeReference dst)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }

            if (dst is GenericParameter genericDst)
            {
                if (src.IsNullable())
                {
                    return false;
                }

                return src.IsCompatibleConstraint(genericDst);
            }

            if (src is GenericParameter genericSrc)
            {
                var srces = genericSrc.Constraints.Select(v => v.ConstraintType);
                if (!srces.Any())
                {
                    return dst.FullName == typeof(object).FullName;
                }

                return srces.Any(v => v.IsCompatible(dst));
            }

            if (src.Is(dst))
            {
                return true;
            }

            if (src is GenericInstanceType genericInstanceSrc &&
                dst is GenericInstanceType genericInstanceDst)
            {
                var srcDef = genericInstanceSrc.ElementType;
                var dstDef = genericInstanceDst.ElementType;
                if (srcDef.Is(dstDef))
                {
                    for (int i = 0; i < genericInstanceSrc.GenericArguments.Count; ++i)
                    {
                        var srcGenArg = genericInstanceSrc.GenericArguments[i];
                        var dstGenArg = genericInstanceDst.GenericArguments[i];
                        var dstGenParam = dstDef.Resolve().GenericParameters[i];
                        var isSrcStruct = srcGenArg.IsStruct();
                        if (dstGenArg is GenericParameter ||
                            (!isSrcStruct && dstGenParam.IsCovariant))
                        {
                            // 共変性の評価
                            if (!srcGenArg.IsCompatible(dstGenArg))
                            {
                                return false;
                            }
                        }
                        else if (!isSrcStruct && dstGenParam.IsContravariant)
                        {
                            // 反変性の評価
                            if (!dstGenArg.IsCompatible(srcGenArg))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            // 不変性の評価
                            if (!srcGenArg.Is(dstGenArg))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            if (src is FunctionPointerType &&
                dst is FunctionPointerType)
            {
                // この時点で同一ではないので不適合
                return false;
            }

            if (src is ArrayType arrayTypeSrc &&
                dst is ArrayType arrayTypeDst)
            {
                if (arrayTypeSrc.Rank != arrayTypeDst.Rank)
                {
                    return false;
                }

                // 配列は共変相当
                return arrayTypeSrc.ElementType.IsCompatible(arrayTypeDst.ElementType);
            }

            if (src is TypeSpecification specTypeSrc &&
                dst is TypeSpecification specTypeDst &&
                specTypeSrc.GetType() == specTypeDst.GetType())
            {
                // Genericだけは受け入れる
                if (specTypeDst.ElementType is GenericParameter)
                {
                    return specTypeSrc.ElementType.IsCompatible(specTypeDst.ElementType);
                }

                return specTypeSrc.ElementType.Is(specTypeDst.ElementType);
            }

            using (ThreadStaticListPool<TypeReference>.Get(out var baseTypes))
            {
                src.GetBaseTypeAndInterfaces(baseTypes);
                foreach (var baseType in baseTypes)
                {
                    if (baseType.IsCompatible(dst))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsCompatibleToByReference(this TypeReference src, ByReferenceType dstByRef)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dstByRef == null)
            {
                throw new ArgumentNullException(nameof(dstByRef));
            }

            return src.IsCompatibleWithoutBase(dstByRef.ElementType);
        }

        public static bool IsCompatibleWithoutBase(this TypeReference src, TypeReference dst)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }

            if (dst is GenericParameter genericDst)
            {
                if (src.IsNullable())
                {
                    return false;
                }

                return src.IsCompatibleConstraint(genericDst);
            }

            if (src.Is(dst))
            {
                return true;
            }

            if (src is GenericInstanceType genericInstanceSrc &&
                dst is GenericInstanceType genericInstanceDst)
            {
                var srcDef = genericInstanceSrc.ElementType;
                var dstDef = genericInstanceDst.ElementType;
                if (srcDef.Is(dstDef))
                {
                    for (int i = 0; i < genericInstanceSrc.GenericArguments.Count; ++i)
                    {
                        var srcGenArg = genericInstanceSrc.GenericArguments[i];
                        var dstGenArg = genericInstanceDst.GenericArguments[i];

                        // Genericだけは受け入れる
                        if (dstGenArg is GenericParameter)
                        {
                            if (!srcGenArg.IsCompatibleWithoutBase(dstGenArg))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (!srcGenArg.Is(dstGenArg))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            if (src is FunctionPointerType &&
                dst is FunctionPointerType)
            {
                // この時点で同一ではないので不適合
                return false;
            }

            if (src is ArrayType arrayTypeSrc &&
                dst is ArrayType arrayTypeDst)
            {
                if (arrayTypeSrc.Rank != arrayTypeDst.Rank)
                {
                    return false;
                }

                // Genericだけは受け入れる
                if (arrayTypeDst.ElementType is GenericParameter)
                {
                    return arrayTypeSrc.ElementType.IsCompatibleWithoutBase(arrayTypeDst.ElementType);
                }

                return arrayTypeSrc.ElementType.Is(arrayTypeDst.ElementType);
            }

            if (src is TypeSpecification specTypeSrc &&
                dst is TypeSpecification specTypeDst &&
                specTypeSrc.GetType() == specTypeDst.GetType())
            {
                // Genericだけは受け入れる
                if (specTypeDst.ElementType is GenericParameter)
                {
                    return specTypeSrc.ElementType.IsCompatibleWithoutBase(specTypeDst.ElementType);
                }

                return specTypeDst.ElementType.Is(specTypeDst.ElementType);
            }

            return false;
        }


        public static bool IsCompatibleConstraint(this TypeReference src, GenericParameter dst)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }

            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }

            // new()
            if (dst.HasNewConstraint() && !src.IsCompatibleNewConstraint())
            {
                return false;
            }

            // class
            if (dst.HasClassConstraint() && !src.IsCompatibleClassConstraint())
            {
                return false;
            }

            // class?
            if (dst.HasClassNullableConstraint() && !src.IsCompatibleClassNullableConstraint())
            {
                return false;
            }

            // unmanaged
            if (dst.HasUnmanagedConstraint() && !src.IsCompatibleUnmanagedConstraint())
            {
                return false;
            }

            // notnull
            if (dst.HasNotNullConstraint() && !src.IsCompatibleNotNullConstraint())
            {
                return false;
            }

            // struct
            if (dst.HasStructConstraint() && !src.IsCompatibleStructConstraint())
            {
                return false;
            }

            // 型制約
            if (dst.HasBaseTypeConstraint() && !src.IsCompatibleBaseTypeConstraint(dst))
            {
                return false;
            }

            // 型?制約
            if (dst.HasBaseTypeNullableConstraint() && !dst.IsCompatibleBaseTypeNullableConstraint(dst))
            {
                return false;
            }

            return true;
        }

        public static bool IsCompatibleNewConstraint(this TypeReference src)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }

            if (src.IsStaticType())
            {
                return false;
            }

            if (src is GenericParameter srcGenericParameter)
            {
                // OK: new() -> new()
                // OK: unmanaged -> new()
                // OK: struct -> new()
                if (srcGenericParameter.HasNewConstraint() ||
                    srcGenericParameter.HasUnmanagedConstraint() ||
                    srcGenericParameter.HasStructConstraint() ||
                    srcGenericParameter.GetBaseTypeConstraints().Any(srcConstraint =>
                    {
                        if (srcConstraint.ConstraintType is not GenericParameter)
                        {
                            return false;
                        }

                        return srcConstraint.ConstraintType.IsCompatibleNewConstraint();
                    }))
                {
                    return true;
                }

                return false;
            }

            return src.HasDefaultConstructor();
        }

        public static bool IsCompatibleClassConstraint(this TypeReference src)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }

            if (src.IsStaticType())
            {
                return false;
            }

            if (src is GenericParameter srcGenericParameter)
            {
                // OK: class -> class
                if (srcGenericParameter.HasClassConstraint() ||
                    srcGenericParameter.GetBaseTypeConstraints().Any(srcConstraint => srcConstraint.ConstraintType.IsCompatibleClassConstraint()))
                {
                    return true;
                }

                return false;
            }

            return src.IsClass();
        }

        public static bool IsCompatibleClassNullableConstraint(this TypeReference src)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (src.IsStaticType())
            {
                return false;
            }

            if (src is GenericParameter srcGenericParameter)
            {
                // OK: class -> class?
                // OK: class? -> class?
                if (srcGenericParameter.HasClassConstraint() ||
                    srcGenericParameter.HasClassNullableConstraint() ||
                    srcGenericParameter.GetBaseTypeConstraints().Any(srcConstraint => srcConstraint.ConstraintType.IsCompatibleClassNullableConstraint()) ||
                    srcGenericParameter.GetBaseTypeNullableConstraints().Any(srcConstraint => srcConstraint.ConstraintType.IsCompatibleClassNullableConstraint()))
                {
                    return true;
                }

                return false;
            }

            return src.IsClass();
        }

        public static bool IsCompatibleUnmanagedConstraint(this TypeReference src)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }

            if (src.IsStaticType())
            {
                return false;
            }

            if (src is GenericParameter srcGenericParameter)
            {
                // OK: unmanaged -> unmanaged
                if (srcGenericParameter.HasUnmanagedConstraint() ||
                    srcGenericParameter.GetBaseTypeConstraints().Any(srcConstraint =>
                    {
                        if (srcConstraint.ConstraintType is not GenericParameter)
                        {
                            return false;
                        }

                        return srcConstraint.ConstraintType.IsCompatibleUnmanagedConstraint();
                    }))
                {
                    return true;
                }

                return false;
            }

            return src.IsUnmanaged();
        }

        public static bool IsCompatibleNotNullConstraint(this TypeReference src)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }

            if (src.IsStaticType())
            {
                return false;
            }

            if (src is GenericParameter srcGenericParameter)
            {
                // OK: class -> notnull
                // OK: notnull -> notnoll
                // OK: unmanaged -> notnull
                // OK: struct -> notnull
                if (srcGenericParameter.HasClassConstraint() ||
                    srcGenericParameter.HasUnmanagedConstraint() ||
                    srcGenericParameter.HasNotNullConstraint() ||
                    srcGenericParameter.HasStructConstraint() ||
                    srcGenericParameter.GetBaseTypeConstraints().Any(srcConstraint => srcConstraint.ConstraintType.IsCompatibleNotNullConstraint()))
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        public static bool IsCompatibleStructConstraint(this TypeReference src)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (src.IsStaticType())
            {
                return false;
            }
            if (src is GenericParameter srcGenericParameter)
            {
                // OK: struct -> struct
                // OK: unmanaged -> struct
                if (srcGenericParameter.HasStructConstraint() ||
                    srcGenericParameter.HasUnmanagedConstraint() ||
                    srcGenericParameter.GetBaseTypeConstraints().Any(srcConstraint =>
                    {
                        if (srcConstraint.ConstraintType is not GenericParameter)
                        {
                            return false;
                        }

                        return srcConstraint.ConstraintType.IsCompatibleStructConstraint();
                    }))
                {
                    return true;
                }
                return false;
            }
            return src.IsStruct();
        }

        public static bool IsCompatibleBaseTypeConstraint(this TypeReference src, GenericParameter dst)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }

            if (src.IsStaticType())
            {
                return false;
            }

            return dst.GetBaseTypeConstraints().All(dstConstraint => src.IsCompatible(dstConstraint.ConstraintType));
        }

        public static bool IsCompatibleBaseTypeNullableConstraint(this TypeReference src, GenericParameter dst)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (dst == null)
            {
                throw new ArgumentNullException(nameof(dst));
            }

            if (src.IsStaticType())
            {
                return false;
            }

            return dst.GetBaseTypeNullableConstraints().All(dstConstraint => src.IsCompatible(dstConstraint.ConstraintType));
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

        public static bool TryGetConstArgument(this Instruction call, MethodReference method, string name, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out var instruction))
            {
                result = null;
                return false;
            }

            return instruction.TryGetConstValue(method, out result);
        }

        public static bool TryGetConstArgument(this Instruction call, MethodReference method, int argNumber, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out var instruction))
            {
                result = null;
                return false;
            }

            return instruction.TryGetConstValue(method, out result);
        }

        public static bool TryGetConstArgument(this Instruction call, MethodReference method, Type type, string name, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(method, type, out result);
        }

        public static bool TryGetConstArgument(this Instruction call, MethodReference method, Type type, int argNumber, out object result)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(method, type, out result);
        }

        public static bool TryGetConstArgument<T>(this Instruction call, MethodReference method, string name, out T result)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(method, out result);
        }

        public static bool TryGetConstArgument<T>(this Instruction call, MethodReference method, int argNumber, out T result)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out var instruction))
            {
                result = default;
                return false;
            }

            return instruction.TryGetConstValue(method, out result);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, MethodReference method, string name, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, out value);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, MethodReference method, int argNumber, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, out value);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, MethodReference method, Type type, string name, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, type, out value);
        }

        public static bool TryGetPushConstArgumentInstruction(this Instruction call, MethodReference method, Type type, int argNumber, out object value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, type, out value);
        }

        public static bool TryGetPushConstArgumentInstruction<T>(this Instruction call, MethodReference method, string name, out T value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(method, out value);
        }

        public static bool TryGetPushConstArgumentInstruction<T>(this Instruction call, MethodReference method, int argNumber, out T value, out Instruction instruction)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(method, out value);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, MethodReference method, string name, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, MethodReference method, int argNumber, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, MethodReference method, Type type, string name, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, type, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions(this Instruction call, MethodReference method, Type type, int argNumber, out object value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out var instruction))
            {
                value = null;
                return false;
            }

            return instruction.TryGetConstValue(method, type, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions<T>(this Instruction call, MethodReference method, string name, out T value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(method, name, out var instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(method, out value, instructions);
        }

        public static bool TryGetPushConstArgumentInstructions<T>(this Instruction call, MethodReference method, int argNumber, out T value, List<Instruction> instructions)
        {
            if (!call.TryGetPushArgumentInstruction(method, argNumber, out var instruction))
            {
                value = default;
                return false;
            }

            return instruction.TryGetConstValue(method, out value, instructions);
        }

        public static bool TryGetPushArgumentInstruction(this Instruction call, MethodReference methodRef, string argName, out Instruction arg)
        {
            arg = null;
            Mono.Collections.Generic.Collection <ParameterDefinition> parameters;
            if (call.OpCode == OpCodes.Call ||
                call.OpCode == OpCodes.Callvirt ||
                call.OpCode == OpCodes.Newobj)
            {
                if (!(call.Operand is MethodReference method))
                {
                    return false;
                }

                var methodDef = method.Resolve();

                if (methodDef != null)
                {
                    parameters = methodDef.Parameters;
                }
                else
                {
                    parameters = method.Parameters;
                }
            }
            else if (call.OpCode == OpCodes.Calli)
            {
                if (!(call.Operand is Mono.Cecil.CallSite callSite))
                {
                    return false;
                }

                parameters = callSite.Parameters;
            }
            else
            {
                return false;
            }

            try
            {
                var argNumber = parameters.Select((v, i) => (v, i)).First(v => v.v.Name == argName).i;
                return TryGetStackPushedInstruction(call, methodRef, argNumber - parameters.Count, out arg);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetPushArgumentInstruction(this Instruction call, MethodReference methodRef, int argNumber, out Instruction arg)
        {
            arg = null;
            Mono.Collections.Generic.Collection <ParameterDefinition> parameters;
            if (call.OpCode == OpCodes.Call ||
                call.OpCode == OpCodes.Callvirt ||
                call.OpCode == OpCodes.Newobj)
            {
                if (!(call.Operand is MethodReference method))
                {
                    return false;
                }

                var methodDef = method.Resolve();
                if (methodDef != null)
                {
                    parameters = methodDef.Parameters;
                }
                else
                {
                    parameters = method.Parameters;
                }
            }
            else if (call.OpCode == OpCodes.Calli)
            {
                if (!(call.Operand is Mono.Cecil.CallSite callSite))
                {
                    return false;
                }

                parameters = callSite.Parameters;
            }
            else
            {
                return false;
            }

            return call.TryGetStackPushedInstruction(methodRef, argNumber - parameters.Count, out arg);
        }

        public static bool TryGetStackPushedInstruction(this Instruction call, MethodReference method, int targetRelativeStackCount, out Instruction result)
        {
            result = null;
            var stackCount = 0;
            var instruction = call.GetPrev();
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

                var popCount = instruction.GetPopCount(method);
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
                return method.HasReturn() ? 1 : 0;
            }

            if (instruction.OpCode == OpCodes.Calli)
            {
                var callSite = instruction.Operand as Mono.Cecil.CallSite;
                return callSite.HasReturn() ? 1 : 0;
            }

            return instruction.OpCode.GetPushCount();
        }

        public static int GetPopCount(this Instruction instruction, MethodReference methodRef)
        {
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            {
                var method = instruction.Operand as MethodReference;
                if (method.IsStatic())
                {
                    return method.Parameters.Count;
                }
                return method.Parameters.Count + 1;
            }

            if (instruction.OpCode == OpCodes.Calli)
            {
                var callSite = instruction.Operand as Mono.Cecil.CallSite;
                return callSite.Parameters.Count + 1;
            }

            if (instruction.OpCode == OpCodes.Newobj)
            {
                var method = instruction.Operand as MethodReference;
                return method.Parameters.Count;
            }

            if (instruction.OpCode == OpCodes.Ret)
            {
                return methodRef.HasReturn() ? 1 : 0;
            }

            return instruction.OpCode.GetPopCount();
        }

        public static int GetPushCount(this OpCode opCode)
        {
            switch (opCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                case StackBehaviour.Varpush:
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
                case StackBehaviour.Varpop:
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
