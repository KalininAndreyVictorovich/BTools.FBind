using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Threading;

namespace BTools
{
    internal static class OneWayBinding<TSource, TDestination>
    {
        // ReSharper disable StaticMemberInGenericType
        private static readonly ConstantExpression trueExpression = Expression.Constant(true);
        private static readonly ConstantExpression nullExpression = Expression.Constant(null);
        // ReSharper restore StaticMemberInGenericType

        public static void BindOneWay(Expression<Func<TSource>> sourceProperty,
            Expression<Func<TDestination>> destinationProperty, BindingSubHandle bindingHandle, Expression<Func<TSource, TDestination>> converter = null,
            SynchronizationContext synchronizationContext = null)
        {
            var memberExpression = destinationProperty.Body as MemberExpression;
            if (memberExpression == null)
                throw new ArgumentException("destinationProperty should be property", nameof(destinationProperty));

            if (converter == null)
                converter = CreateConverter();

            var getterWithConverter = Expression.Invoke(converter, Expression.Invoke(sourceProperty));

            var getterWithConverterAndGuard = Expression.TryCatch(
                getterWithConverter,
                Expression.Catch(
                    typeof (Exception),
                    Expression.Invoke((Expression<Func<TDestination>>)(() => default(TDestination)))));

            var assignExpression = CreateSafeAssignExpression(memberExpression, getterWithConverterAndGuard);
            var assignNullExpression = CreateSafeAssignExpression(memberExpression,
                Expression.Convert(Expression.Constant(default(TDestination)), typeof(TDestination)));

            var setter = Expression.Lambda<Action>(assignExpression).Compile();
            var nullSetter = Expression.Lambda<Action>(assignNullExpression).Compile();

            var propertyChain = GetPropertyChain(sourceProperty);

            var setterHandler = new SetterHandler(propertyChain.ReversedProperties[0].Name, setter, nullSetter,
                bindingHandle, synchronizationContext);

            PropertyChangeHandler handler = setterHandler;

            for (int i = 1; i < propertyChain.ReversedProperties.Count; i++)
            {
                handler = new TraverseHandler(propertyChain.ReversedProperties[i], handler);
            }

            handler.Subscribe(propertyChain.Root, skipNotify: false);
            bindingHandle.Subscribe(handler);
        }

        private static Expression<Func<TSource, TDestination>> CreateConverter()
        {
            if (typeof (TDestination).IsAssignableFrom(typeof (TSource)))
                return x => (TDestination)(object)x;

            if (Nullable.GetUnderlyingType(typeof (TDestination)) != null)
            {
                return x => (TDestination)new NullableConverter(typeof(TDestination)).ConvertFrom(x);
            }

            return
                x => x is TDestination
                    ? (TDestination)(object)x
                    : x is IConvertible
                        ? (TDestination)Convert.ChangeType(x, typeof (TDestination))
                        : default(TDestination);
        }

        private static Expression CreateSafeAssignExpression(MemberExpression left, Expression right)
        {
            var notNullCondition = NotNullMemberExpression(left.Expression);
            var nullGuard = Expression.IfThen(notNullCondition, Expression.Assign(left, right));

            return nullGuard;
        }

        private static Expression NotNullMemberExpression(Expression expression)
        {
            Expression notNullExpression;
            if (expression.Type.IsValueType)
            {
                notNullExpression = trueExpression;
            }
            else
            {
                notNullExpression = Expression.NotEqual(expression, nullExpression);
            }
            var memberExpression = expression as MemberExpression;
            if (memberExpression == null) return notNullExpression;

            return Expression.AndAlso(NotNullMemberExpression(memberExpression.Expression), notNullExpression);
        }

        private static PropertyChain GetPropertyChain<T>(Expression<Func<T>> sourceProperty)
        {
            var propertyChain = new List<System.Reflection.PropertyInfo>(1);

            // cyclically parse expression tree to extract sequence of properties.
            // Property chain is reversed - rightmost property first.
            Expression expression = sourceProperty.Body;
            while (expression != null)
            {
                if (expression is UnaryExpression && expression.NodeType == ExpressionType.Convert)
                    expression = ((UnaryExpression)expression).Operand;

                var propertyExpression = expression as MemberExpression;
                if (propertyExpression?.Member is System.Reflection.PropertyInfo)
                {
                    propertyChain.Add((System.Reflection.PropertyInfo)propertyExpression.Member);
                    expression = propertyExpression.Expression;
                }
                else
                {
                    break;
                }
            }
            // At this moment propertyChain contains chain of properties, expression - expression to get bind object (this in most cases)

            if (propertyChain.Count < 1 || expression == null)
                throw new ArgumentException("Please provide a lambda expression like '() => PropertyName.ChildProperty'");

            // Get master object from master object expression.
            var constantExpression = expression as ConstantExpression;
            object root = constantExpression != null
                ? constantExpression.Value
                : Expression.Lambda(expression).Compile().DynamicInvoke();

            if (root == null)
                throw new ArgumentException("Cannot binding for null objects");

            return new PropertyChain
            {
                Root = root,
                ReversedProperties = propertyChain
            };
        }


        private abstract class PropertyChangeHandler: IDisposable
        {
            protected object container;
            private readonly string propertyName;

            protected PropertyChangeHandler(string propertyName)
            {
                this.propertyName = propertyName;
            }

            protected abstract void OnPropertyChange(bool skipNotify);

            public void Subscribe(object newMasterObject, bool skipNotify)
            {
                var inpc = container as INotifyPropertyChanged;
                if (inpc != null)
                    inpc.PropertyChanged -= container_PropertyChanged;
                else if (container != null)
                {
                    var eventInfo = container.GetType().GetEvent(propertyName + "Changed");
                    if (eventInfo != null)
                        eventInfo.RemoveEventHandler(container, new EventHandler(container_SpecifiedPropertyChanged));
                }
                this.container = newMasterObject;
                inpc = container as INotifyPropertyChanged;
                if (inpc != null)
                    inpc.PropertyChanged += container_PropertyChanged;
                else if (container != null)
                {
                    var eventInfo = container.GetType().GetEvent(propertyName + "Changed");
                    if (eventInfo != null)
                        eventInfo.AddEventHandler(container, new EventHandler(container_SpecifiedPropertyChanged));
                }
                OnPropertyChange(skipNotify);
            }

            private void container_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == propertyName)
                    OnPropertyChange(skipNotify: false);
            }

            private void container_SpecifiedPropertyChanged(object sender, EventArgs e)
            {
                OnPropertyChange(skipNotify: false);
            }

            void IDisposable.Dispose()
            {
                Subscribe(null, skipNotify: true);
            }
        }

        private class SetterHandler: PropertyChangeHandler
        {
            private readonly Action setter;
            private readonly Action nullSetter;
            private readonly SynchronizationContext synchronizationContext;
            private readonly BindingSubHandle bindingHandle;

            public SetterHandler(string propertyName, Action setter, Action nullSetter, BindingSubHandle bindingHandle,
                SynchronizationContext synchronizationContext = null)
                : base(propertyName)
            {
                this.setter = setter;
                this.nullSetter = nullSetter;
                this.bindingHandle = bindingHandle;
                this.synchronizationContext = synchronizationContext;
            }

            protected override void OnPropertyChange(bool skipNotify)
            {
                if (skipNotify) return;
                if (bindingHandle.IsLocked) return;

                if (synchronizationContext == null)
                {
                    InvokeSetter(null);
                }
                else
                {
                    // used to transfer original call stack to simplify debugging
                    object invokeState = null;

                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        invokeState = new
                        {
                           Environment.StackTrace
                        };
                    }

                    synchronizationContext.Post(InvokeSetter, invokeState);
                }
            }

            private void InvokeSetter(object state)
            {
                bindingHandle.LockSibling();
                try
                {
                    (container == null ? nullSetter : setter).Invoke();
                }
                catch
                {
                    // ignored
                }
                bindingHandle.UnLockSibling();
            }
        }

        private class TraverseHandler: PropertyChangeHandler
        {
            private readonly PropertyChangeHandler handler;
            private readonly System.Reflection.PropertyInfo property;

            public TraverseHandler(System.Reflection.PropertyInfo property, PropertyChangeHandler handler)
                : base(property.Name)
            {
                this.property = property;
                this.handler = handler;
            }

            protected override void OnPropertyChange(bool skipNotify)
            {
                object newTarget = null;
                if (container != null && property != null)
                    newTarget = property.GetValue(container, null);
                handler.Subscribe(newTarget, skipNotify);
            }
        }


        private struct PropertyChain
        {
            public object Root;
            public IList<System.Reflection.PropertyInfo> ReversedProperties;
        }
    }
}