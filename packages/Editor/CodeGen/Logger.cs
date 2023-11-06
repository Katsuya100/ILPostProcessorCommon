using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class Logger
    {
        private static readonly string CurrentPath = $"{Environment.CurrentDirectory.Replace("\\", "/")}/";

        private string _logPath;
        private List<DiagnosticMessage> _messages = new List<DiagnosticMessage>();
        public List<DiagnosticMessage> Messages => _messages;

        public Logger(Type type, ICompiledAssembly assembly)
        {
            _logPath = $"Logs/{type.Name}/{assembly.Name}.txt";
            var dir = Path.GetDirectoryName(_logPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.Open(_logPath, FileMode.Create, FileAccess.Write, FileShare.Read).Dispose();
        }

        private void LogInternal(DiagnosticType type, object o, string file, int line, int column, string stacktrace)
        {
            file = file.Replace("\\", "/").Replace(CurrentPath, string.Empty).Trim();
            var logType = type == 0 ? string.Empty : $"{type} ";
            var log = $"{file}({line},{column}):{logType}{o}\n{stacktrace}";
            using (var f = File.AppendText(_logPath))
            {
                f.WriteLine(log);
            }

            Console.WriteLine(log);

            if (type != 0)
            {
                _messages.Add(new DiagnosticMessage()
                {
                    DiagnosticType = type,
                    MessageData = $"{o}",
                    File = file,
                    Line = line,
                    Column = column,
                });
            }
        }

        private void LogInternal(DiagnosticType type, object o, IEnumerable<(string file, string method, int line)> frames)
        {
            var useFrames = TrimStack(frames);
            var stackLog = FormatStack(useFrames);

            var frame = useFrames.FirstOrDefault(v => !string.IsNullOrEmpty(v.file));
            var file = frame.file;
            var line = frame.line;
            var column = 0;

            LogInternal(type, o, file, line, column, stackLog);
        }

        private void LogInternal(DiagnosticType type, object o, IEnumerable<StackFrame> frames)
        {
            var useFrames = TrimStack(frames);
            var stackLog = FormatStack(useFrames);

            var frame = useFrames.FirstOrDefault(v => !string.IsNullOrEmpty(v.GetFileName()));
            var file = frame?.GetFileName() ?? string.Empty;
            var line = frame?.GetFileLineNumber() ?? 0;
            var column = frame?.GetFileColumnNumber() ?? 0;

            LogInternal(type, o, file, line, column, stackLog);
        }

        private void LogInternal(DiagnosticType type, object o, string file, int line, int column, IEnumerable<StackFrame> frames)
        {
            var useFrames = TrimStack(frames);
            var stackLog = FormatStack(useFrames);
            LogInternal(type, o, file, line, column, stackLog);
        }

        private void LogInternal(DiagnosticType type, object o, string file, int line, int column, IEnumerable<(string file, string method, int line)> frames)
        {
            var useFrames = TrimStack(frames);
            var stackLog = FormatStack(useFrames);
            LogInternal(type, o, file, line, column, stackLog);
        }

        private void LogInternal(DiagnosticType type, object o)
        {
            var stack = new StackTrace(true);
            var frames = stack.GetFrames();
            LogInternal(type, o, frames);
        }

        private void LogInternal(DiagnosticType type, object o, string file, int line, int column)
        {
            var stack = new StackTrace(true);
            var frames = stack.GetFrames();
            LogInternal(type, o, file, line, column, frames);
        }

        private void LogInternal(DiagnosticType type, object o, string target)
        {
            LogInternal(type, $"{o}  at {target}");
        }

        private void LogInternal(DiagnosticType type, object o, string target, string file, int line, int column)
        {
            LogInternal(type, $"{o}  at {target}", file, line, column);
        }

        private void LogInternal(DiagnosticType type, object o, MemberReference member)
        {
            LogInternal(type, o, ILPostProcessorUtils.GetMemberName(member));
        }

        private void LogInternal(DiagnosticType type, object o, MemberReference member, string file, int line, int column)
        {
            LogInternal(type, o, ILPostProcessorUtils.GetMemberName(member), file, line, column);
        }

        private void LogInternal(DiagnosticType type, object o, MemberInfo member)
        {
            LogInternal(type, o, ILPostProcessorUtils.GetMemberName(member));
        }

        private void LogInternal(DiagnosticType type, object o, MemberInfo member, string file, int line, int column)
        {
            LogInternal(type, o, ILPostProcessorUtils.GetMemberName(member), file, line, column);
        }

        public void Log(DiagnosticType type, object o)
        {
            LogInternal(type, o);
        }

        public void Log(DiagnosticType type, object o, MethodDefinition method, Instruction instruction)
        {
            SequencePoint point = null;
            for (var it = instruction; it != null && point == null; it = it.Previous)
            {
                point = method.DebugInformation.GetSequencePoint(it);
            }

            if (point == null)
            {
                LogInternal(type, o, method, string.Empty, 0, 0);
                return;
            }

            Log(type, o, point);
        }

        public void Log(DiagnosticType type, Exception e)
        {
            Log(type, $"{e.GetType()}:{e.Message}", e.StackTrace);
        }

        public void Log(DiagnosticType type, object o, MemberInfo member)
        {
            LogInternal(type, o, member, string.Empty, 0, 0);
        }

        public void Log(DiagnosticType type, object o, MemberReference member)
        {
            MethodDefinition method = null;
            if (member is MethodReference methodRef)
            {
                if (!(member is MethodDefinition methodTmp))
                {
                    methodTmp = methodRef.Resolve();
                }

                method = methodTmp;
            }
            else if (member is PropertyReference propertyRef)
            {
                if (!(propertyRef is PropertyDefinition property))
                {
                    property = propertyRef.Resolve();
                }

                method = property?.GetMethod ?? property?.SetMethod;
            }

            if (method != null)
            {
                var point = method.DebugInformation.SequencePoints.FirstOrDefault();
                if (point != null)
                {
                    Log(type, o, point);
                    return;
                }
            }

            LogInternal(type, o, member, string.Empty, 0, 0);
        }

        public void Log(DiagnosticType type, object o, string stacktrace)
        {
            var splitedStackTraces = stacktrace.Split('\n');
            var stackframes = splitedStackTraces.Select(Parse);
            LogInternal(type, o, stackframes);
        }

        public void Log(DiagnosticType type, object o, SequencePoint point)
        {
            if (point == null)
            {
                LogInternal(type, o, string.Empty, 0, 0);
                return;
            }

            var fileName = point.Document.Url;
            var line = point.StartLine;
            var column = point.StartColumn;
            LogInternal(type, o, fileName, line, column);
        }

        public void Log(DiagnosticType type, object o, StackFrame frame)
        {
            if (frame == null)
            {
                LogInternal(type, o, string.Empty, 0, 0);
                return;
            }

            var fileName = frame.GetFileName();
            var line = frame.GetFileLineNumber();
            var column = frame.GetFileColumnNumber();
            LogInternal(type, o, fileName, line, column);
        }

        public void Log(DiagnosticType type, object o, string file, int line, int column)
        {
            LogInternal(type, o, file, line, column);
        }

        private static IEnumerable<StackFrame> TrimStack(IEnumerable<StackFrame> frames)
        {
            var indices = frames.Select((v, i) => (v, i));
            var firstIndex = indices.FirstOrDefault(v => !IsLogMethod(v.v)).i;
            var lastIndex = indices.FirstOrDefault(v => IsILPostProcessorRoot(v.v)).i;
            var result = frames.Take(lastIndex + 1).Skip(firstIndex);
            return result;
        }

        private static IEnumerable<(string file, string method, int line)> TrimStack(IEnumerable<(string file, string method, int line)> frames)
        {
            var indices = frames.Select((v, i) => (v, i));
            var firstIndex = indices.FirstOrDefault(v => !IsLogMethod(v.v)).i;
            var lastIndex = indices.FirstOrDefault(v => IsILPostProcessorRoot(v.v)).i;
            var result = frames.Take(lastIndex + 1).Skip(firstIndex);
            return result;
        }

        private static string FormatStack(IEnumerable<StackFrame> frames)
        {
            var result = string.Concat(frames.Select(FormatStack));
            return result;
        }

        private static string FormatStack(IEnumerable<(string file, string method, int line)> frames)
        {
            var result = string.Concat(frames.Select(FormatStack));
            return result;
        }

        private static string FormatStack(StackFrame frame)
        {
            var method = frame.GetMethod();
            var methodName = ILPostProcessorUtils.GetMethodName(method);
            var fileName = frame.GetFileName()?.Replace("\\", "/")?.Replace(CurrentPath, string.Empty).Trim() ?? string.Empty;
            var lineNumber = frame.GetFileLineNumber();
            return $"{methodName} (at {fileName}:{lineNumber})\n";
        }

        private static string FormatStack((string file, string method, int line) frame)
        {
            var methodName = frame.method;
            var fileName = frame.file?.Replace("\\", "/")?.Replace(CurrentPath, string.Empty).Trim() ?? string.Empty;
            var lineNumber = frame.line;
            return $"{methodName} (at {fileName}:{lineNumber})\n";
        }

        private static bool IsLogMethod(StackFrame frame)
        {
            var method = frame.GetMethod();
            var reflectedType = method.ReflectedType;
            return reflectedType == typeof(Logger) ||
                   (reflectedType == typeof(ILPostProcessorUtils) && method.Name.StartsWith(nameof(ILPostProcessorUtils.Log)));
        }

        private static bool IsLogMethod((string file, string method, int line) frame)
        {
            return frame.method.StartsWith("Katuusagi.ConstExpressionForUnity.Editor.ILPostProcessorUtils.Log") ||
                   frame.method.StartsWith("Katuusagi.ConstExpressionForUnity.Editor.Logger");
        }

        private static bool IsILPostProcessorRoot(StackFrame frame)
        {
            var method = frame.GetMethod();
            var reflectedType = method.ReflectedType;
            return typeof(ILPostProcessor).IsAssignableFrom(reflectedType) &&
                   (method.Name == nameof(ILPostProcessor.Process) || method.Name == nameof(ILPostProcessor.WillProcess));
        }

        private static bool IsILPostProcessorRoot((string file, string method, int line) frame)
        {
            return frame.method == "Katuusagi.ConstExpressionForUnity.Editor.ConstExpressionILPostProcessor.Process(ICompiledAssembly compiledAssembly)";
        }

        private (string file, string method, int line) Parse(string stack)
        {
            string file = string.Empty;
            string method = string.Empty;
            int line = 0;
            try
            {
                var methodTop = stack.Substring(6, stack.Length - 6);
                var splited = methodTop.Split(") in ");
                if (splited.Length <= 1)
                {
                    method = methodTop.Trim();
                }
                else
                {
                    method = $"{splited[0]})";
                    splited = splited[1].Split(":line ");
                    file = splited[0].Trim();
                    int.TryParse(splited[1], out line);
                }
            }
            catch
            {
            }

            return (file, method, line);
        }
    }
}
