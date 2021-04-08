using System;
using System.Linq.Expressions;
using System.Threading;

namespace BTools
{
    /// <summary>
    /// Static class with methods to setup bindings
    /// </summary>
    public static class FBinding
    {
        /// <summary>
        /// Setup one way binding.
        /// <paramref name="destinationProperty"/> will be set to <paramref name="sourceProperty"/> on every property change.
        /// </summary>
        /// <param name="sourceProperty">Source property for subscription</param>
        /// <param name="destinationProperty">Destination property that will be set on every source property change</param>
        /// <param name="converter">Optional source to destination value converter.
        /// If not provided, System.Convert.ChangeType will be used</param>
        /// <returns>Unsubscription handle.
        /// When disposed, all change notification subscriptions in source property chain will be removed.</returns>
        /// <example>
        /// <code>
        /// partial class SampleForm : System.Windows.Forms.System.Windows.Forms
        /// {
        ///     public SampleForm()
        ///     {
        ///         InitializeComponent();
        ///         FBinding.BindOneWay(() => ViewModel.User.BirthDate, () => UserBirthDateLabel.Text, date => date.ToString("d"));
        ///     }
        ///
        ///     private SampleViewModel viewModel;
        ///     public SampleViewModel ViewModel
        ///     {
        ///         get { return viewModel; }
        ///         set
        ///         {
        ///             viewModel = value;
        ///             ViewModelChanged?.Invoke(this, EventArgs.Empty);
        ///         }
        ///     }
        ///     public event EventHandler ViewModelChanged;
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Change notification subscriptions is done for every object in the property chain. In the exaple above
        /// UserBirthDateLabel.Text will be updated when User property of ViewModel of ViewModel of this will change.
        /// </para>
        /// <para>
        /// Change notification subscription support <see cref="System.ComponentModel.INotifyPropertyChanged"/>
        /// and {PropertyName}Change event pattern.
        /// </para>
        /// </remarks>
        public static IDisposable BindOneWay<TSource, TDestination>(
        Expression<Func<TSource>> sourceProperty,
            Expression<Func<TDestination>> destinationProperty,
            Expression<Func<TSource, TDestination>> converter = null)
        {
            if (sourceProperty == null) throw new ArgumentNullException(nameof(sourceProperty));
            if (destinationProperty == null) throw new ArgumentNullException(nameof(destinationProperty));

            var bindingHandle = new BindingHandle();
            OneWayBinding<TSource, TDestination>.BindOneWay(sourceProperty, destinationProperty,
                bindingHandle.Subhandle1, converter);
            return bindingHandle;
        }

        /// <summary>
        /// Setup one way bindind.
        /// <paramref name="destinationProperty"/> will be set to <paramref name="sourceProperty"/> on every property change.
        /// Assigned will be performed in <see cref="SynchronizationContext"/> captured on this method call.
        /// </summary>
        /// <param name="sourceProperty">Source property for subscription</param>
        /// <param name="destinationProperty">Destination property that will be set on every source property change</param>
        /// <param name="converter">Optional source to destination value converter.
        /// If not provided, System.Convert.ChangeType will be used</param>
        /// <returns>Unsubscription handle.
        /// When disposed, all change notification subscriptions in source property chain will be removed.</returns>
        /// <example>
        /// <code>
        /// partial class SampleForm : System.Windows.Forms.System.Windows.Forms
        /// {
        ///     public SampleForm()
        ///     {
        ///         InitializeComponent();
        ///         FBinding.BindOneWaySync(() => ViewModel.User.BirthDate, () => UserBirthDateLabel.Text, date => date.ToString("d"));
        ///     }
        ///
        ///     private SampleViewModel viewModel;
        ///     public SampleViewModel ViewModel
        ///     {
        ///         get { return viewModel; }
        ///         set
        ///         {
        ///             viewModel = value;
        ///             ViewModelChanged?.Invoke(this, EventArgs.Empty);
        ///         }
        ///     }
        ///     public event EventHandler ViewModelChanged;
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Change notification subscriptions is done for every object in the property chain. In the exaple above
        /// UserBirthDateLabel.Text will be updated when User property of ViewModel of ViewModel of this will change.
        /// </para>
        /// <para>
        /// Change notification subscription support <see cref="System.ComponentModel.INotifyPropertyChanged"/>
        /// and {PropertyName}Change event pattern.
        /// </para>
        /// </remarks>
        public static IDisposable BindOneWaySync<TSource, TDestination>(
            Expression<Func<TSource>> sourceProperty,
            Expression<Func<TDestination>> destinationProperty,
            Expression<Func<TSource, TDestination>> converter = null)
        {
            if (sourceProperty == null) throw new ArgumentNullException(nameof(sourceProperty));
            if (destinationProperty == null) throw new ArgumentNullException(nameof(destinationProperty));

            var bindingHandle = new BindingHandle();

            OneWayBinding<TSource, TDestination>.BindOneWay(sourceProperty, destinationProperty,
                bindingHandle.Subhandle1, converter, SynchronizationContext.Current);
            return bindingHandle;
        }

        /// <summary>
        /// Setup two way bindind. Equivalent of two <see cref="BindOneWay{TSource,TDestination}"/>, but with cycle prevention.
        /// </summary>
        public static IDisposable BindTwoWay<T1, T2>(
            Expression<Func<T1>> property1,
            Expression<Func<T2>> property2)
        {
            // ReSharper disable once IntroduceOptionalParameters.Global
            // When using optional parameter user can specify only one converter by a mistake.
            return BindTwoWay(property1, property2, null, null);
        }

        /// <summary>
        /// Setup two way bindind. Equivalent of two <see cref="BindOneWay{TSource,TDestination}"/>, but with cycle prevention.
        /// </summary>
        public static IDisposable BindTwoWay<TSource, TDestination>(
            Expression<Func<TSource>> sourceProperty,
            Expression<Func<TDestination>> destinationProperty,
            Expression<Func<TSource, TDestination>> converter,
            Expression<Func<TDestination, TSource>> converterBack)
        {
            if (sourceProperty == null) throw new ArgumentNullException(nameof(sourceProperty));
            if (destinationProperty == null) throw new ArgumentNullException(nameof(destinationProperty));

            var bindingHandle = new BindingHandle();

            OneWayBinding<TSource, TDestination>.BindOneWay(sourceProperty, destinationProperty,
                bindingHandle.Subhandle1, converter);
            OneWayBinding<TDestination, TSource>.BindOneWay(destinationProperty, sourceProperty,
                bindingHandle.Subhandle2, converterBack);

            return bindingHandle;
        }

        /// <summary>
        /// Setup two way bindind. Equivalent of two <see cref="BindOneWaySync{TSource,TDestination}"/>, but with cycle prevention.
        /// </summary>
        public static IDisposable BindTwoWaySync<TSource, TDestination>(
            Expression<Func<TSource>> sourceProperty,
            Expression<Func<TDestination>> destinationProperty)
        {
            // ReSharper disable once IntroduceOptionalParameters.Global
            // Using optional parameter user can specify only one converter by a mistake.
            return BindTwoWaySync(sourceProperty, destinationProperty, null, null);
        }

        /// <summary>
        /// Setup two way bindind. Equivalent of two <see cref="BindOneWaySync{TSource,TDestination}"/>, but with cycle prevention.
        /// </summary>
        public static IDisposable BindTwoWaySync<TSource, TDestination>(
            Expression<Func<TSource>> sourceProperty,
            Expression<Func<TDestination>> destinationProperty,
            Expression<Func<TSource, TDestination>> converter,
            Expression<Func<TDestination, TSource>> converterBack)
        {
            if (sourceProperty == null) throw new ArgumentNullException(nameof(sourceProperty));
            if (destinationProperty == null) throw new ArgumentNullException(nameof(destinationProperty));

            var synchronizationContext = SynchronizationContext.Current;
            var bindingHandle = new BindingHandle();

            OneWayBinding<TSource, TDestination>.BindOneWay(sourceProperty, destinationProperty,
                bindingHandle.Subhandle1, converter, synchronizationContext);
            OneWayBinding<TDestination, TSource>.BindOneWay(destinationProperty, sourceProperty,
                bindingHandle.Subhandle2, converterBack, synchronizationContext);

            return bindingHandle;
        }
    }
}
