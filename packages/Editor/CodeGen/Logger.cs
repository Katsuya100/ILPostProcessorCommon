using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Katuusagi.ILPostProcessorCommon.Editor
{
    public class Logger
    {
        private static readonly Regex StackTraceCheck = new Regex("at .*\\(.*\\) in .*\\:line [0-9]{1,}");
        private static readonly Regex StackTracePrefix = new Regex("at .*\\(.*\\) in ");
        private static readonly Regex StackTraceSuffix = new Regex(" in .*\\:line [0-9]{1,}");

        private List<DiagnosticMessage> _messages = new List<DiagnosticMessage>();
        public List<DiagnosticMessage> Messages => _messages;

        public void Clear()
        {
            _messages.Clear();
        }

        public void LogWarning(object o)
        {
            _messages.Add(new DiagnosticMessage()
            {
                DiagnosticType = DiagnosticType.Warning,
                MessageData = $"{o}",
            });
        }

        public void LogWarning(object o, MethodDefinition method, Instruction instruction)
        {
            SequencePoint point = null;
            for (var it = instruction; it != null && point == null; it = it.Previous)
            {
                point = method.DebugInformation.GetSequencePoint(it);
            }

            if (point == null)
            {
                LogWarning($"{o}  at {ILPostProcessorUtils.GetMethodName(method)}");
                return;
            }

            var file = point.Document.Url.Replace("\\", "/");
            file = file.Remove(0, file.IndexOf("/Assets/") + 1);
            LogWarning($"{o}", file, point.StartLine, point.StartColumn);
        }

        public void LogWarning(object o, MethodInfo method)
        {
            LogWarning($"{o}  at {ILPostProcessorUtils.GetMethodName(method)}");
        }

        public void LogWarning(object o, string stacktrace)
        {
            var splitedStackTraces = stacktrace.Split('\n');
            stacktrace = splitedStackTraces.FirstOrDefault(v => StackTraceCheck.IsMatch(v));
            if (stacktrace == null)
            {
                stacktrace = splitedStackTraces.FirstOrDefault();
                LogWarning($"{o}{stacktrace}");
                return;
            }

            var method = StackTraceSuffix.Replace(stacktrace, string.Empty);
            stacktrace = StackTracePrefix.Replace(stacktrace, string.Empty);
            var traceElements = stacktrace.Split(":line ");
            var file = traceElements[0].Replace("\\", "/");
            file = file.Remove(0, file.IndexOf("/Assets/") + 1);
            int.TryParse(traceElements[1], out var line);
            LogWarning($"{o}{method}", file, line, 0);
        }

        public void LogWarning(object o, string file, int line, int column)
        {
            _messages.Add(new DiagnosticMessage()
            {
                DiagnosticType = DiagnosticType.Warning,
                MessageData = o.ToString(),
                File = file,
                Line = line,
                Column = column,
            });
        }

        public void LogError(object o)
        {
            _messages.Add(new DiagnosticMessage()
            {
                DiagnosticType = DiagnosticType.Error,
                MessageData = $"{o}",
            });
        }

        public void LogError(object o, MethodDefinition method, Instruction instruction)
        {
            SequencePoint point = null;
            for (var it = instruction; it != null && point == null; it = it.Previous)
            {
                point = method.DebugInformation.GetSequencePoint(it);
            }

            if (point == null)
            {
                LogError($"{o}  at {ILPostProcessorUtils.GetMethodName(method)}");
                return;
            }

            var file = point.Document.Url.Replace("\\", "/");
            file = file.Remove(0, file.IndexOf("/Assets/") + 1);
            LogError($"{o}", file, point.StartLine, point.StartColumn);
        }

        public void LogError(object o, MethodInfo method)
        {
            LogError($"{o}  at {ILPostProcessorUtils.GetMethodName(method)}");
        }

        public void LogError(object o, string stacktrace)
        {
            var splitedStackTraces = stacktrace.Split('\n');
            stacktrace = splitedStackTraces.FirstOrDefault(v => StackTraceCheck.IsMatch(v));
            if (stacktrace == null)
            {
                stacktrace = splitedStackTraces.FirstOrDefault();
                LogError($"{o}{stacktrace}");
                return;
            }

            var method = StackTraceSuffix.Replace(stacktrace, string.Empty);
            stacktrace = StackTracePrefix.Replace(stacktrace, string.Empty);
            var traceElements = stacktrace.Split(":line ");
            var file = traceElements[0].Replace("\\", "/");
            file = file.Remove(0, file.IndexOf("/Assets/") + 1);
            int.TryParse(traceElements[1], out var line);
            LogError($"{o}{method}", file, line, 0);
        }

        public void LogError(object o, string file, int line, int column)
        {
            _messages.Add(new DiagnosticMessage()
            {
                DiagnosticType = DiagnosticType.Error,
                MessageData = o.ToString(),
                File = file,
                Line = line,
                Column = column,
            });
        }

        public void LogException(Exception e)
        {
            LogError($"{e.GetType()}:{e.Message}", e.StackTrace);
        }
    }
}
