using System;

namespace SoopChatNet.Data
{
    public sealed class Notice : ConcurrentPool<Notice>, IPoolable
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
