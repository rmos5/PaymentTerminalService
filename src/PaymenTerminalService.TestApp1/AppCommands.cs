using System;
using System.Windows.Input;

namespace PaymentTerminalService.TestApp1
{
    public abstract class AppCommandBase : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public abstract bool CanExecute(object parameter);

        public abstract void Execute(object parameter);

        public void RaiseCanExecuteChanged()
        {
            EventHandler h = CanExecuteChanged;
            h?.Invoke(this, EventArgs.Empty);
        }
    }

    public abstract class AppCommandBase<T> : AppCommandBase
            where T : IContext
    {
        public T Context { get; }

        protected AppCommandBase(T context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            Context = context;
        }

        public override bool CanExecute(object parameter)
        {
            return !Context.IsBusy;
        }
    }
}
