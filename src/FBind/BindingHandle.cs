using System;
using System.Threading;

namespace BTools
{
    internal class BindingHandle: IDisposable
    {
        public BindingHandle()
        {
            var pair = BindingSubHandle.NewPair();
            Subhandle1 = pair.Item1;
            Subhandle2 = pair.Item2;
        }

        public BindingSubHandle Subhandle1 { get; }
        public BindingSubHandle Subhandle2 { get; }

        public void Dispose()
        {
            Subhandle1.Dispose();
            Subhandle2.Dispose();
        }
    }

    internal class BindingSubHandle: IDisposable
    {
        private int isLocked;
        private BindingSubHandle sibling;
        private IDisposable subscription;

        private BindingSubHandle()
        {
        }

        public static Tuple<BindingSubHandle, BindingSubHandle> NewPair()
        {
            var sub1 = new BindingSubHandle();
            var sub2 = new BindingSubHandle();
            sub1.sibling = sub2;
            sub2.sibling = sub1;
            return new Tuple<BindingSubHandle, BindingSubHandle>(sub1, sub2);
        }

        public void Subscribe(IDisposable subscription)
        {
            if (this.subscription != null)
                throw new InvalidOperationException("BindingSubHandle already subscribed");
            this.subscription = subscription;
        }

        public bool IsLocked
        {
            get { return isLocked != 0; }
        }

        public void LockSibling()
        {
            Interlocked.Increment(ref sibling.isLocked);
        }

        public void UnLockSibling()
        {
            Interlocked.Decrement(ref sibling.isLocked);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}

