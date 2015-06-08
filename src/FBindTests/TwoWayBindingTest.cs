using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using BTools;
using Xunit;

namespace BToolsTests
{
    public class TwoWayBindingTest
    {
        [Fact]
        public void Binding()
        {
            var source = new ContainerInpc<int>();
            var destination = new ContainerEvent<string>();

            FBinding.BindTwoWay(() => source.Value, () => destination.Value);

            source.Value = 42;
            Assert.Equal("42", destination.Value);

            destination.Value = "0";
            Assert.Equal(0, source.Value);
        }

        [Fact]
        public void BindingWithConverter()
        {
            var source = new ContainerInpc<int>();
            var destination = new ContainerEvent<string>();

            FBinding.BindTwoWay(() => source.Value, () => destination.Value,
                x => (x - 1).ToString(), x => int.Parse(x) + 1);

            source.Value = 42;
            Assert.Equal("41", destination.Value);

            destination.Value = "12";
            Assert.Equal(13, source.Value);
        }

        [Fact]
        public void Chained()
        {
            var source = new ContainerInpc<IContainer<int>>();
            var destination = new ContainerEvent<IContainer<string>> {Value = new Container<string>()};

            FBinding.BindTwoWay(() => source.Value.Value, () => destination.Value.Value);

            source.Value = new ContainerInpc<int> {Value = 42};
            Assert.Equal("42", destination.Value.Value);

            destination.Value = new ContainerEvent<string> {Value = "12"};
            Assert.Equal(12, source.Value.Value);
        }

        [Fact]
        public void MustNotLeak()
        {
            var refs = MakeUnreferencedBinding();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.False(refs.Item1.IsAlive);
            Assert.False(refs.Item2.IsAlive);
        }

        [Fact]
        public void SyncUseSynchronizationContext()
        {
            var source = new ContainerInpc<int>();
            var destination = new StringContainerSetterCaptureThread();
            Thread loopThread;
            using (var synchronizationContextMock = new SynchronizationContextMock())
            {
                loopThread = synchronizationContextMock.LoopThread;
                SynchronizationContext.SetSynchronizationContext(synchronizationContextMock);

                FBinding.BindTwoWaySync(() => source.Value, () => destination.StringProperty);
                var thread = new Thread(() =>
                {
                    source.Value = 42;
                });
                thread.IsBackground = true;
                thread.Start();
                thread.Join();
            }
            Assert.Equal(loopThread, destination.Thread);
            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void NoBackSetting()
        {
            var source = new ContainerInpc<int>();
            var destination = new ContainerInpc<int>();

            FBinding.BindTwoWay(() => source.Value, () => destination.Value, x => x + 1, x => x);

            source.Value = 42;
            Assert.Equal(43, destination.Value);
            Assert.Equal(42, source.Value);
        }

        [Fact]
        public void NoCycleSettingInSync()
        {
            var source = new ContainerInpc<int>();
            var destination = new ContainerInpc<int>();

            using (var synchronizationContextMock = new SynchronizationContextMock())
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContextMock);

                FBinding.BindTwoWaySync(() => source.Value, () => destination.Value, x => x + 1, x => x);
                var thread = new Thread(() =>
                {
                    source.Value = 42;
                });
                thread.IsBackground = true;
                thread.Start();
                thread.Join();
            }

            Assert.Equal(43, destination.Value);
            Assert.Equal(42, source.Value);
        }

        [Fact]
        public void DisposeStopBinding()
        {
            var source = new ContainerInpc<int>();
            var destination = new ContainerInpc<int>();

            using (FBinding.BindTwoWay(() => source.Value, () => destination.Value))
            {
                source.Value = 42;
                Assert.Equal(42, destination.Value);

                destination.Value = 43;
                Assert.Equal(43, source.Value);
            }
            source.Value = 45;
            Assert.Equal(43, destination.Value);

            destination.Value = 50;
            Assert.Equal(45, source.Value);
        }


        private Tuple<WeakReference,WeakReference> MakeUnreferencedBinding()
        {
            // This moved to separate method to garantee 
            // optimizer not removed cleanup

            var source = new ContainerInpc<IContainer<int>> {Value = new ContainerEvent<int> {Value = 42}};
            var destination = new StringContainer {StringProperty = "0"};

            FBinding.BindTwoWay(() => source.Value.Value, () => destination.StringProperty);

            var sourceReference = new WeakReference(source);
            var destinationReference = new WeakReference(destination);

            return new Tuple<WeakReference, WeakReference>(sourceReference, destinationReference);
        }

        private interface IContainer<T>
        {
            // ReSharper disable once UnusedMemberInSuper.Global
            T Value { get; set; }
        }

        private class Container<T>: IContainer<T>
        {
            public T Value { get; set; }
        }

        private class ContainerInpc<T>: INotifyPropertyChanged, IContainer<T>
        {
            private T value;
            public event PropertyChangedEventHandler PropertyChanged;

            public T Value
            {
                get { return value; }
                set
                {
                    if (Equals(this.value, value))
                        return;
                    this.value = value;
                    OnPropertyChanged();
                }
            }

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private class ContainerEvent<T>: IContainer<T>
        {
            private T value;
            // ReSharper disable once EventNeverSubscribedTo.Local
            public event EventHandler ValueChanged;

            public T Value
            {
                get { return value; }
                set
                {
                    if (Equals(this.value, value))
                        return;
                    this.value = value;
                    if (ValueChanged != null)
                        ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        private class StringContainer
        {
            public string StringProperty { get; set; }
        }

        private class StringContainerSetterCaptureThread
        {
            private string stringProperty;
            public Thread Thread { get; private set; }

            public string StringProperty
            {
                get { return stringProperty; }
                // ReSharper disable once UnusedMember.Local
                set
                {
                    stringProperty = value;
                    Thread = Thread.CurrentThread;
                }
            }
        }
    }
}