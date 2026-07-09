using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Updog.Unity
{
    internal static class UpdogStacktraceParser
    {
        private static readonly Regex UnityFrameRegex = new Regex(
            @"^(?<function>.*?) \(at (?<file>.*):(?<line>\d+)\)$",
            RegexOptions.Compiled);

        public static List<UpdogStackFrame> Parse(string stackTrace)
        {
            var frames = new List<UpdogStackFrame>();

            if (string.IsNullOrWhiteSpace(stackTrace))
            {
                return frames;
            }

            var lines = stackTrace.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (ShouldSkip(trimmed))
                {
                    continue;
                }

                var match = UnityFrameRegex.Match(trimmed);
                if (match.Success)
                {
                    frames.Add(new UpdogStackFrame
                    {
                        function = match.Groups["function"].Value.Trim(),
                        file = CleanFilePath(match.Groups["file"].Value),
                        line = int.Parse(match.Groups["line"].Value)
                    });
                }
                else
                {
                    frames.Add(new UpdogStackFrame
                    {
                        function = trimmed,
                        file = ""
                    });
                }
            }

            return frames;
        }

        private static bool ShouldSkip(string frame)
        {
            return frame.StartsWith("UnityEngine.Debug:", StringComparison.Ordinal) ||
                   frame.StartsWith("UnityEngine.Logger:", StringComparison.Ordinal) ||
                   frame.StartsWith("UnityEngine.DebugLogHandler:", StringComparison.Ordinal);
        }

        private static string CleanFilePath(string file)
        {
            return string.IsNullOrWhiteSpace(file)
                ? ""
                : file.Trim().TrimStart('.', '/', '\\').Replace('\\', '/');
        }
    }
}
