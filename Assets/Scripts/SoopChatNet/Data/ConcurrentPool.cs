using System;
using System.Collections.Concurrent;

namespace SoopChatNet.Data
{
    public abstract class ConcurrentPool<T> where T : class, IPoolable, new()
    {
        protected static readonly ConcurrentQueue<T> _pool = new ConcurrentQueue<T>();

        public static T Rent(ReadOnlySpan<char> span)
        {
            T obj = !_pool.TryDequeue(out obj) ? new T() : obj;
            try
            {
                obj.Parse(span);
                return obj;
            }
            catch
            {
                // Return half-initialized object to the pool so callers can't leak it
                obj.Reset();
                _pool.Enqueue(obj);
                throw;
            }
        }

        public static void Release(T obj)
        {
            obj.Reset();
            _pool.Enqueue(obj);
        }

        protected static ReadOnlySpan<char> Next(ReadOnlySpan<char> span, ref int start)
        {
            if (start > span.Length)
                throw new FormatException("Parse cursor out of range.");

            int idx = span[start..].IndexOf('\f');
            if (idx < 0)
            {
                // no trailing delimiter — consume the remainder
                ReadOnlySpan<char> tail = span[start..];
                start = span.Length;
                return tail;
            }

            ReadOnlySpan<char> token = span.Slice(start, idx);
            start += idx + 1;
            return token;
        }

        public abstract void Parse(ReadOnlySpan<char> span);

        public abstract void Reset();
    }
}
