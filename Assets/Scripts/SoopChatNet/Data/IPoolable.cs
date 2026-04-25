using System;

namespace SoopChatNet.Data
{
    public interface IPoolable
    {
        void Parse(ReadOnlySpan<char> span);
        void Reset();
    }
}
