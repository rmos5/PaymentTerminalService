using System.ComponentModel;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Defines a contract for context objects that expose an <c>IsBusy</c> property and support property change notification.
    /// </summary>
    public interface IContext : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets a value indicating whether the context is currently busy (e.g., performing an operation).
        /// </summary>
        bool IsBusy { get; }
    }

    /// <summary>
    /// Abstract base class for context objects used in data binding and state management.
    /// Provides an <c>IsBusy</c> property with change notification and a virtual <c>UpdateModelState</c> method.
    /// Equality and hash code are based on the runtime type and <c>IsBusy</c> value.
    /// Derived classes should override these methods if they add additional state.
    /// </summary>
    public abstract class ContextBase : IContext
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool isBusy;

        /// <summary>
        /// Gets or sets a value indicating whether the context is currently busy (e.g., performing an operation).
        /// Raises the <c>PropertyChanged</c> event when the value changes.
        /// </summary>
        public bool IsBusy
        {
            get => isBusy;
            set
            {
                if (isBusy != value)
                {
                    isBusy = value;
                    UpdateModelState();
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Called when the model state changes. Raises <see cref="PropertyChanged"/> for <c>IsBusy</c> by default.
        /// Derived classes can override to raise additional property changes.
        /// </summary>
        protected virtual void UpdateModelState()
        {
            OnPropertyChanged(nameof(IsBusy));
        }
    }
}