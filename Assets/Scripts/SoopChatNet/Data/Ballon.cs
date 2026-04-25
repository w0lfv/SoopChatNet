using System;

namespace SoopChatNet.Data
{
    public sealed class Ballon : ConcurrentPool<Ballon>, IPoolable
    {
        public string sender;
        public string nickname;
        public string message;
        public int amount;
        public string uuid;
        public int fanOrder;
        public bool isDefault;
        public bool isTopFan;

        public override void Parse(ReadOnlySpan<char> span)
        {
            int start = 1;

            Next(span, ref start); // skip : bjid
            sender = Next(span, ref start).ToString();
            nickname = Next(span, ref start).ToString();
            amount = int.Parse(Next(span, ref start));
        }

        public override void Reset()
        {
            sender = nickname = message = uuid = null;
            amount = fanOrder = 0;
            isDefault = isTopFan = false;
        }

    }
}
