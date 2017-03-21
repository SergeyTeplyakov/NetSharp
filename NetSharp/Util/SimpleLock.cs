using System;
using System.Threading;

namespace NetSharp
{
    // ээээээ.... а чем простой лок не подходит?
    class SimpleLock : IDisposable
    {
        ManualResetEventSlim mres;
        int waiters;

        public SimpleLock()
        {
            mres = new ManualResetEventSlim(false);
        }

        public void Enter()
        {
            if (Interlocked.Increment(ref waiters) == 1)
                return;
            else
                mres.Reset();

            mres.Wait();
        }

        public void Leave()
        {
            if (Interlocked.Decrement(ref waiters) == 0)
                return;

            mres.Set();
        }

        public void Dispose()
        {
            if (mres != null)
            {
                mres.Dispose();
                mres = null;
            }
        }

        ~SimpleLock()
        {
            // Это совсем неправильно. Почитай про Dispose паттерн.
            // Есть и у меня на блоге.
            // Но лучше - не городить огород.
            Dispose();
        }
    }
}
