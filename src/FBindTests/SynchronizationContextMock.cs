using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace BToolsTests
{
    public class SynchronizationContextMock: SynchronizationContext, IDisposable
    {
        private readonly ConcurrentQueue<Tuple<SendOrPostCallback, object>> queue =
            new ConcurrentQueue<Tuple<SendOrPostCallback, object>>();

        private readonly AutoResetEvent waitEvent = new AutoResetEvent(false);
        private bool disposed;

        public SynchronizationContextMock()
        {
            LoopThread = new Thread(Loop) {IsBackground = true};
            LoopThread.Start(null);
        }

        public Thread LoopThread { get; private set; }

        #region Implementation of IDisposable

        void IDisposable.Dispose()
        {
            disposed = true;
            GC.SuppressFinalize(this);
            waitEvent.Set();
            LoopThread.Join();
            Debug.Assert(queue.IsEmpty);
        }

        #endregion

        private void Loop(object dummy)
        {
            while (!disposed)
            {
                waitEvent.WaitOne();

                Tuple<SendOrPostCallback, object> callback;
                while (queue.TryDequeue(out callback))
                {
                    callback.Item1.Invoke(callback.Item2);
                }
            }
        }

        #region Overrides of SynchronizationContext

        public override void Post(SendOrPostCallback d, object state)
        {
            queue.Enqueue(Tuple.Create<SendOrPostCallback, object>(d, state));
            waitEvent.Set();
        }

        #endregion

        ~SynchronizationContextMock()
        {
            disposed = true;
        }
    }
}