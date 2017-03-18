using System;
using System.Collections.Generic;
using System.Threading;
using NetSharp.ServerSide;

namespace NetSharp.UnitTest.Infrastructure
{
    class HostEventListener : IDisposable
    {
        Host host;
        ManualResetEventSlim mres;
        IStrategyUnlock strategyUnlock;
        List<EventData> events;

        public HostEventListener(Host host)
        {
            mres = new ManualResetEventSlim();
            host.HostStateChange += Host_HostStateChange;
            events = new List<EventData>();
            this.host = host;
        }

        private void Host_HostStateChange(object sender, HostStateChangeArgs e)
        {
            if (strategyUnlock != null && strategyUnlock.Unlock(sender, e))
                mres.Set();
            else
                events.Add(new EventData(sender, e));
        }

        public void SetStrategyUnlock(IStrategyUnlock strategyUnlock)
        {
            if (strategyUnlock == null)
                throw new ArgumentNullException(nameof(strategyUnlock));

            this.strategyUnlock = strategyUnlock;

            for (int i = 0; i < events.Count; i++)
            {
                if (strategyUnlock.Unlock(events[i].Sender, events[i].Args))
                {
                    mres.Set();
                    break;
                }
            }
        }

        public void Wait()
        {
            mres.Wait();
        }

        public void Reset()
        {
            mres.Reset();
        }

        public void Dispose()
        {
            host.HostStateChange -= Host_HostStateChange;
            events.Clear();
        }

        private class EventData
        {
            public readonly object Sender;
            public readonly HostStateChangeArgs Args;

            public EventData(object sender, HostStateChangeArgs args)
            {
                Sender = sender;
                Args = args;
            }
        }
    }

    interface IStrategyUnlock
    {
        bool Unlock(object sender, HostStateChangeArgs e);
    }

    class UnlockIfRemoveLastHandler : IStrategyUnlock
    {
        public bool Unlock(object sender, HostStateChangeArgs e)
        {
            if (e.Reason == ReasonChange.RemoveHandler)
                return ((ConnectedClient)sender).HandlerCount == 0;
            else
                return false;
        }
    }

    class UnlockIfRemoveLastClient : IStrategyUnlock
    {
        public bool Unlock(object sender, HostStateChangeArgs e)
        {
            if (e.Reason == ReasonChange.RemoveClient)
                return ((ConnectedClientManager)sender).ClientCount == 0;
            else
                return false;
        }
    }
}
