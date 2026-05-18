using PaymentTerminalService.Model;
using System;
using System.Diagnostics;
using System.Windows;
using WPFAndNETHelpers;
using WPFHelpers;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void ShowErrorMessage(Exception exception, string title = "Error")
        {
            const int detailsMessgageLength = 500;
            const string detailsMessageSuffix = "...";
            string message = exception.GetAllMessages().Truncate(detailsMessgageLength, detailsMessageSuffix);
            string details = null;

            if (exception is ApiException<ErrorResponse> apiException && apiException.Result != null)
            {
                message = apiException.Result.Message ?? message;
#if DEBUG
                details = apiException.Result.Details?.ToString();
                if (details?.Length > 0)
                {
                    details = $"{details.Truncate(detailsMessgageLength, detailsMessageSuffix)}";
                    message += $"\n{details}";
                }
#endif
            }

            Trace.WriteLine($"{nameof(ShowErrorMessage)}:{title} {message}{(string.IsNullOrEmpty(details) ? "" : "\n" + details)}", GetType().FullName);

            using (new MessageBoxCenterer(MainWindow))
            {
                MessageBox.Show(MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            ShowErrorMessage(e.Exception);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowErrorMessage(e.Exception);
        }
    }
}
