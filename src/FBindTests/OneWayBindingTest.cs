using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using BTools;
using Xunit;

namespace BToolsTests
{
    public class OneWayBindingTest
    {
        [Fact]
        public void Inpc()
        {
            var source = new ContainerInpc<int>();
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty);

            source.Value = 42;

            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void InpcWithConverter()
        {
            var source = new ContainerInpc<int>();
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty, x => (-x).ToString());

            source.Value = 42;

            Assert.Equal("-42", destination.StringProperty);
        }

        [Fact]
        public void BindPropertyOfComplexType()
        {
            var source = new ContainerInpc<int[]>();
            var destination = new ContainerInpc<int[]>();

            FBinding.BindOneWay(() => source.Value, () => destination.Value);

            source.Value = new[] {42};

            Assert.Equal(source.Value, destination.Value);
        }

        [Fact]
        public void BindPropertyOfComplexTypeToBaseType()
        {
            var source = new ContainerInpc<int[]>();
            var destination = new ContainerInpc<object>();

            FBinding.BindOneWay(() => source.Value, () => destination.Value);

            source.Value = new[] {42};

            Assert.Equal(source.Value, destination.Value);
        }


        [Fact]
        public void BindPropertyOfComplexTypeToImplementedInterface()
        {
            var source = new ContainerInpc<int[]>();
            var destination = new ContainerInpc<IList>();

            FBinding.BindOneWay(() => source.Value, () => destination.Value);

            source.Value = new[] {42};

            Assert.Equal(source.Value, destination.Value);
        }

        [Fact]
        public void ConverterUsesRuntimeType()
        {
            var source1 = new ContainerInpc<object>();
            var source2 = new ContainerInpc<object>();
            var destination1 = new ContainerInpc<string>();
            var destination2 = new ContainerInpc<IList>();

            FBinding.BindOneWay(() => source1.Value, () => destination1.Value);
            FBinding.BindOneWay(() => source2.Value, () => destination2.Value);

            source1.Value = 42;
            source2.Value = new[] { 42 };

            Assert.Equal("42", destination1.Value);
            Assert.Equal(source2.Value, destination2.Value);
        }

        [Fact]
        public void SetDefaultValueIfConverterFail()
        {
            var source = new ContainerInpc<object>();
            var destination = new ContainerInpc<int> {Value = 1};

            FBinding.BindOneWay(() => source.Value, () => destination.Value);

            source.Value = new[] { 42 };

            Assert.Equal(default(int), destination.Value);
        }

        [Fact]
        public void SetDefaultValueIfSourceIsNull()
        {
            var source = new ContainerInpc<string> {Value = "12"};
            var destination = new ContainerInpc<int> {Value = 1};

            FBinding.BindOneWay(() => source.Value, () => destination.Value);

            source.Value = null;

            Assert.Equal(default(int), destination.Value);
        }

        [Fact]
        public void ChangedEvent()
        {
            var source = new ContainerEvent<int>();
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty);

            source.Value = 42;

            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void ChangedEventWithConverter()
        {
            var source = new ContainerEvent<int>();
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty, x => (x + 1).ToString());

            source.Value = 42;

            Assert.Equal("43", destination.StringProperty);
        }

        [Fact]
        public void InpcChained()
        {
            var source = new ContainerInpc<IContainer<int>>();
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value.Value, () => destination.StringProperty);

            source.Value = new ContainerInpc<int> {Value = 42};

            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void ChangedEventChained()
        {
            var source = new ContainerEvent<IContainer<int>>();
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value.Value, () => destination.StringProperty);

            source.Value = new ContainerEvent<int> {Value = 42};

            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void StripedChainBindOnParentUpdate()
        {
            // Suppose we have binding ViewModel.User.FirstName -> UserFirstName.Text
            // After ViewModel.User = new User {FirstName = "John"} 
            // UserFirstName.Text Should contain "John" even if User do not implement INPC

            var source = new ContainerInpc<IContainer<IContainer<int>>>
            {
                Value = new Container<IContainer<int>>
                {
                    Value = new Container<int>
                    {
                        Value = 1
                    }
                }
            };
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value.Value.Value, () => destination.StringProperty);

            source.Value = new Container<IContainer<int>>
            {
                Value = new Container<int>
                {
                    Value = 42
                }
            };

            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void NullInChainSetDefaultValue()
        {
            var destination = new Container<int>();
            var source = new ContainerEvent<IContainer<IContainer<int>>>
            {
                Value = new ContainerEvent<IContainer<int>>
                {
                    Value = new ContainerEvent<int>
                    {
                        Value = 42
                    }
                }
            };

            FBinding.BindOneWay(() => source.Value.Value.Value, () => destination.Value);


            source.Value = null;

            Assert.Equal(0, destination.Value);
        }

        [Fact]
        public void ConvertsValueToNullableType()
        {
            var source1 = new ContainerInpc<object>();
            var source2 = new ContainerInpc<object>() {Value = 8};
            var source3 = new ContainerInpc<object>();
            var source4 = new ContainerInpc<int?>();
            var destination1 = new ContainerInpc<int?>();
            var destination2 = new ContainerInpc<int?>();
            var destination3 = new ContainerInpc<int?>();
            var destination4 = new ContainerInpc<int?>();

            FBinding.BindOneWay(() => source1.Value, () => destination1.Value);
            FBinding.BindOneWay(() => source2.Value, () => destination2.Value);
            FBinding.BindOneWay(() => source3.Value, () => destination3.Value);
            FBinding.BindOneWay(() => source4.Value, () => destination4.Value);

            destination2.Value = 4;

            source1.Value = "42";
            source2.Value = null;
            source3.Value = 43;
            source4.Value = 45;

            Assert.Equal(42, destination1.Value);
            Assert.Equal(null, destination2.Value);
            Assert.Equal(43, destination3.Value);
            Assert.Equal(45, destination4.Value);
        }


        [Fact]
        public void BindWhenRegister()
        {
            var source = new ContainerEvent<int> {Value = 42};
            var destination = new StringContainer();

            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty);

            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void ModifiedClosureUndefinedBehaviour()
        {
            var source = new ContainerInpc<int>();
            var destination = new StringContainer();

            // ReSharper disable once AccessToModifiedClosure
            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty);
            
            var sourceCopy = source;

            source = null;

            sourceCopy.Value = 42;

            Assert.NotEqual("42", destination.StringProperty);
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

                FBinding.BindOneWaySync(() => source.Value, () => destination.StringProperty);
                var thread = new Thread(() =>
                {
                    source.Value = 42;
                });
                thread.Start();
                thread.Join();
            }
            Assert.Equal(loopThread, destination.Thread);
            Assert.Equal("42", destination.StringProperty);
        }

        [Fact]
        public void DisposeStopBinding()
        {
            var source = new ContainerInpc<int>();
            var destination = new ContainerInpc<int>();


            using (FBinding.BindOneWay(() => source.Value, () => destination.Value))
            {
                source.Value = 42;
            }
            source.Value = 43;

            Assert.Equal(42, destination.Value);
        }

        [Fact]
        public void CallSetterOnEveryOverlappingChangeNotification()
        {
            var source = new ContainerInpc<int>();
            var destination = new StringContainer();

            var inWait = new ManualResetEvent(false);
            FBinding.BindOneWay(() => source.Value, () => destination.StringProperty, x => SlowConvert(x, inWait, null));
            inWait.Reset();

            var thread1 = new Thread(() =>
            {
                source.Value = 1;
            });
            var thread2 = new Thread(() =>
            {
                inWait.WaitOne();
                Thread.Sleep(TimeSpan.FromMilliseconds(100)); // Not the best way to fight race condition... cannot figure out a better one
                source.Value = 2;
            });
            thread1.Start();
            thread2.Start();
            thread1.Join();
            thread2.Join();

            Assert.Equal("2", destination.StringProperty);
        }

        [Fact]
        public void CallSetterOnEveryOverlappingChangeNotificationSync()
        {
            var source = new ContainerInpc<int>();
            var destination = new StringContainer();

            using (var synchronizationContextMock = new SynchronizationContextMock())
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContextMock);

                var inWait = new ManualResetEvent(false);
                var outWait = new ManualResetEvent(false);
                FBinding.BindOneWaySync(() => source.Value, () => destination.StringProperty, x => SlowConvert(x, inWait, outWait));
                outWait.WaitOne();
                inWait.Reset();

                var thread1 = new Thread(() =>
                {
                    source.Value = 1;
                });
                var thread2 = new Thread(() =>
                {
                    inWait.WaitOne();
                    source.Value = 2;
                });
                thread1.Start();
                thread2.Start();
                thread1.Join();
                thread2.Join();
            }

            Assert.Equal("2", destination.StringProperty);
        }

        private Tuple<WeakReference,WeakReference> MakeUnreferencedBinding()
        {
            // This moved to separate method to garantee 
            // optimizer not removed cleanup

            var source = new ContainerInpc<IContainer<int>> {Value = new ContainerEvent<int> {Value = 42}};
            var destination = new StringContainer {StringProperty = "0"};

            FBinding.BindOneWay(() => source.Value.Value, () => destination.StringProperty);

            var sourceReference = new WeakReference(source);
            var destinationReference = new WeakReference(destination);

            return new Tuple<WeakReference, WeakReference>(sourceReference, destinationReference);
        }

        private string SlowConvert(int i, ManualResetEvent inWait, ManualResetEvent outWait = null)
        {
            inWait.Set();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            if (outWait != null) outWait.Set();
            return i.ToString();
        }

        private interface IContainer<out T>
        {
            T Value { get; }
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