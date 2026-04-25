using System;

namespace SoopChatNet.Data
{
    public sealed class Chat : ConcurrentPool<Chat>, IPoolable
    {
        public string sender;
        public string nickname;
        public int permission;
        public string message;
        public int lang;
        public int subMonth;
        public UserStatusFlag1 flag1;
        public UserStatusFlag2 flag2;

        public override void Parse(ReadOnlySpan<char> span)
        {
            int start = 1;
            message = Next(span, ref start).ToString();
            sender = Next(span, ref start).ToString();
            Next(span, ref start); // skip
            permission = int.Parse(Next(span, ref start));
            lang = int.Parse(Next(span, ref start));
            nickname = Next(span, ref start).ToString();

            if (permission == 0 || permission == 3)
            {
                ReadOnlySpan<char> flags = Next(span, ref start);
                int sep = flags.IndexOf('|');
                if (sep < 0)
                {
                    flag1 = UserStatusFlag1.NONE;
                    flag2 = UserStatusFlag2.NONE;
                }
                else
                {
                    flag1 = (UserStatusFlag1)long.Parse(flags[..sep]);
                    flag2 = (UserStatusFlag2)long.Parse(flags[(sep + 1)..]);
                }
            }
        }

        public override void Reset()
        {
            sender = nickname = message = null;
            permission = 0;
            lang = subMonth = 0;
            flag1 = UserStatusFlag1.NONE;
            flag2 = UserStatusFlag2.NONE;
        }
    }
}
