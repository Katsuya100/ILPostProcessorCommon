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
        private struct Frame
        {
            public string File;
            public string Method;
            public int Line;
            public int Column;
        }

        private static readonly string CurrentPath = $"{Environment.CurrentDirectory.Replace("\\", "/")}/";

        private Type _rootType;
        private string _logPath;
        private List<DiagnosticMessage> _messages = new List<DiagnosticMessage>();
        public List<DiagnosticMessage> Messages => _messages;

        public Logger(Type type, ICompiledAssembly assembly)
        {
            _rootType = type;
            _logPath = $"Logs/{type.Name}/{assembly.Name}.txt";
            var dir = Path.GetDirectoryName(_logPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.Open(_logPath, FileMode.Create, FileAccess.Write, FileShare.Read).Dispose();
        }

        private void Write(object o)
        {
            using (var f = File.AppendText(_logPath))
            {
                f.WriteLine(o);
            }
        }

        private void LogRaw(DiagnosticType type, string id, string title, object o, string file, int line, int column, IEnumerable<Frame> frames)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = type.ToString();
            }

            if (string.IsNullOrEmpty(title))
            {
                title = type.ToString();
            }

            if (frames == null)
            {
                frames = GetStack();
            }

            frames = TrimStack(frames);

            if (file == null)
            {
                var frame = frames.FirstOrDefault(v => !string.IsNullOrEmpty(v.File));
                file = frame.File;
                line = frame.Line;
                column = frame.Column;
            }

            if (string.IsNullOrEmpty(file))
            {
                file = " ";
            }

            file = file.Replace("\\", "/").Replace(CurrentPath, string.Empty).Trim();
            var stacktrace = FormatStack(frames);

            var logType = type == 0 ? "Info" : type.ToString();
            var log = $"{file}({line},{column}):{logType} {id}:{o}\n{stacktrace}";
            Write(log);

            Console.WriteLine(log);

            if (type != 0)
            {
                _messages.Add(new DiagnosticMessage()
                {
                    DiagnosticType = type,
                    MessageData = $"{id}:{o}",
                    File = file,
                    Line = line,
                    Column = column,
                });
            }
        }

        private void LogInternal(DiagnosticType type, string id, string title, object o, string file, int line, int column, IEnumerable<Frame> frames = null)
        {
            if (file == null)
            {
                file = " ";
            }

            LogRaw(type, id, title, o, file, line, column, frames);
        }

        private void LogInternal(DiagnosticType type, string id, string title, object o, IEnumerable<Frame> frames = null)
        {
            LogRaw(type, id, title, o, null, 0, 0, frames);
        }

        private void LogInternal(DiagnosticType type, string id, string title, object o, SequencePoint point, IEnumerable<Frame> frames = null)
        {
            if (point == null)
            {
                LogInternal(type, id, title, o, string.Empty, 0, 0, frames);
                return;
            }
            LogRaw(type, id, title, o, point.Document.Url, point.StartLine, point.StartColumn, frames);
        }

        private void LogInternal(DiagnosticType type, string id, string title, object o, MemberReference member)
        {
            if (member == null)
            {
                LogInternal(type, id, title, o, string.Empty, 0, 0);
                return;
            }

            var point = member.GetSequencePoint();
            if (point == null)
            {
                LogInternal(type, id, title, $"{o}  (at {ILPPUtils.GetMemberName(member)})", string.Empty, 0, 0);
                return;
            }

            LogInternal(type, id, title, o, point);
        }

        private void LogInternal(DiagnosticType type, string id, string title, object o, MemberInfo member)
        {
            if (member == null)
            {
                LogInternal(type, id, title, o, string.Empty, 0, 0);
                return;
            }

            LogInternal(type, id, title, $"{o}  (at {ILPPUtils.GetMemberName(member)})", string.Empty, 0, 0);
        }

        public void Log(DiagnosticType type, object o)
        {
            LogInternal(type, string.Empty, string.Empty, o);
        }

        public void Log(DiagnosticType type, string id, string title, object o)
        {
            LogInternal(type, id, title, o);
        }

        public void Log(DiagnosticType type, string id, string title, object o, MethodDefinition method, Instruction instruction)
        {
            if (method == null)
            {
                LogInternal(type, id, title, o, string.Empty, 0, 0);
                return;
            }

            var point = method.GetSequencePoint(instruction);
            if (point == null)
            {
                LogInternal(type, id, title, o, method);
                return;
            }

            LogInternal(type, id, title, o, point);
        }

        public void Log(DiagnosticType type, string id, string title, object o, MemberReference member)
        {
            LogInternal(type, id, title, o, member);
        }

        public void Log(DiagnosticType type, string id, string title, object o, MemberInfo member)
        {
            LogInternal(type, id, title, o, member);
        }

        public void Log(DiagnosticType type, string id, string title, object o, SequencePoint point)
        {
            LogInternal(type, id, title, o, point);
        }

        public void Log(DiagnosticType type, string id, string title, object o, string file, int line, int column)
        {
            LogInternal(type, id, title, o, file, line, column);
        }

        public void Log(DiagnosticType type, Exception e)
        {
            if (e == null)
            {
                LogInternal(type, "Exception", "Exception", "Unknown Exception");
                return;
            }

            var frames = ParseStack(e.StackTrace);
            LogInternal(type, "Exception", "Exception", $"{e.GetType()}:{e.Message}", frames);
        }

        private IEnumerable<Frame> ParseStack(string stackTrace)
        {
            var splitedStackTraces = stackTrace.Split('\n');
            return splitedStackTraces.Select(Parse);
        }

        private IEnumerable<Frame> GetStack()
        {
            var stack = new StackTrace(true);
            return stack.GetFrames().Select(v => new Frame()
            {
                File = v?.GetFileName(),
                Method = ILPPUtils.GetMethodName(v?.GetMethod()),
                Line = v?.GetFileLineNumber() ?? 0,
                Column = v?.GetFileColumnNumber() ?? 0
            });
        }

        private IEnumerable<Frame> TrimStack(IEnumerable<Frame> frames)
        {
            var indices = frames.Select((v, i) => (v, i));
            var firstIndex = indices.FirstOrDefault(v => !IsLogMethod(v.v)).i;
            var lastIndex = indices.FirstOrDefault(v => IsRoot(v.v)).i;
            var result = frames.Take(lastIndex + 1).Skip(firstIndex);
            return result;
        }

        private string FormatStack(IEnumerable<Frame> frames)
        {
            var result = string.Concat(frames.Select(FormatStack));
            return result;
        }

        private string FormatStack(Frame frame)
        {
            var methodName = frame.Method;
            var fileName = frame.File?.Replace("\\", "/")?.Replace(CurrentPath, string.Empty).Trim() ?? string.Empty;
            var lineNumber = frame.Line;
            return $"{methodName} (at {fileName}:{lineNumber})\n";
        }

        private bool IsLogMethod(Frame frame)
        {
            return frame.Method.StartsWith($"{typeof(ILPPUtils).FullName}.Log") ||
                   frame.Method.StartsWith(typeof(Logger).FullName);
        }

        private bool IsRoot(Frame frame)
        {
            return frame.Method.StartsWith($"{_rootType.FullName}.{nameof(ILPostProcessor.Process)}({nameof(ICompiledAssembly)}") ||
                   frame.Method.StartsWith($"{_rootType.FullName}.{nameof(ILPostProcessor.WillProcess)}({nameof(ICompiledAssembly)}");
        }

        private Frame Parse(string stack)
        {
            string file = string.Empty;
            string method = string.Empty;
            int line = 0;
            try
            {
                var methodTop = stack.Substring(6, stack.Length - 6);
                var splited = methodTop.Split(new string[] { ") in " }, StringSplitOptions.None);
                if (splited.Length <= 1)
                {
                    method = methodTop.Trim();
                }
                else
                {
                    method = $"{splited[0]})";
                    splited = splited[1].Split(new string[] { ":line " }, StringSplitOptions.None);
                    file = splited[0].Trim();
                    int.TryParse(splited[1], out line);
                }
            }
            catch
            {
            }

            return new Frame()
            {
                File = file,
                Method = method,
                Line = line,
            };
        }

    }
}
