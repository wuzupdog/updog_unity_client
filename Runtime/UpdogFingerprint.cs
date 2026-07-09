using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Updog.Unity
{
    internal static class UpdogFingerprint
    {
        public static string Generate(string errorClass, IReadOnlyCollection<UpdogStackFrame> frames)
        {
            var builder = new StringBuilder(errorClass ?? "");

            if (frames != null && frames.Count > 0)
            {
                foreach (var frame in frames.Take(5))
                {
                    builder.Append('|')
                        .Append(frame.file ?? "")
                        .Append(':')
                        .Append(frame.function ?? "");
                }
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BytesToHex(bytes);
            }
        }

        private static string BytesToHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                chars[i * 2] = GetHexValue(b / 16);
                chars[i * 2 + 1] = GetHexValue(b % 16);
            }

            return new string(chars);
        }

        private static char GetHexValue(int value)
        {
            return (char)(value < 10 ? value + '0' : value - 10 + 'a');
        }
    }
}
