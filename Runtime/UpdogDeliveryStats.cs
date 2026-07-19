using System.Collections.Generic;

namespace Updog.Unity
{
    public sealed class UpdogDeliveryStats
    {
        public long Queued { get; internal set; }
        public long Sent { get; internal set; }
        public long Retried { get; internal set; }
        public IDictionary<string, long> Dropped { get; internal set; }
        public int QueueRecords { get; internal set; }
        public int QueueBytes { get; internal set; }
        public int InFlight { get; internal set; }
    }
}
