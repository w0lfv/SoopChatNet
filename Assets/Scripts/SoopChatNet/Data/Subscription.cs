using System;

namespace SoopChatNet.Data
{
    public sealed class Subscription : ConcurrentPool<Subscription>, IPoolable
    {
        // TODO: replace with parsed fields once raw data format is confirmed
        public string raw;

        public override void Parse(ReadOnlySpan<char> span)
        {
            raw = span.ToString();
        }

        public override void Reset()
        {
            raw = null;
        }
    }
}
