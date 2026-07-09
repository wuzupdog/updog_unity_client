using System;

namespace Updog.Unity
{
    [Serializable]
    public sealed class UpdogStackFrame
    {
        public string file;
        public string function;
        public int? line;
    }
}
