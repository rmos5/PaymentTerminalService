using PaymentTerminalService.Model;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Modal prompt dialog for terminal operator interaction.
    /// Shows a yes/no confirmation when <see cref="Prompt.YesNo"/> is true,
    /// or a validated text input when an <see cref="PromptInputSpec"/> is provided.
    /// </summary>
    public partial class PromptDialog : Window
    {
        private readonly bool _isYesNo;
        private readonly PromptInputSpec _inputSpec;

        /// <summary>
        /// Gets the text entered by the operator. Only meaningful when <see cref="PromptInputSpec"/> was provided.
        /// </summary>
        public string Input => InputTextBox.Text;

        /// <summary>
        /// Gets a value indicating whether the dialog was closed by the backend aborting the prompt,
        /// rather than by explicit user action. When <see langword="true"/>, the caller should skip
        /// calling RespondToPrompt — the server has already moved on.
        /// </summary>
        public bool IsAborted { get; private set; }

        public PromptDialog(Prompt prompt)
        {
            Trace.WriteLine($"{nameof(PromptDialog)}: promptId={prompt?.PromptId}", GetType().FullName);

            InitializeComponent();

            MessageText.Text = prompt?.Message;
            _isYesNo = prompt?.YesNo == true;
            _inputSpec = prompt?.Input;

            if (_isYesNo)
            {
                // Yes/No mode — Yes confirms, No is the safe cancellation default.
                OkButton.Content = "Yes";
                OkButton.IsDefault = true;
                CancelButton.Content = "No";
                CancelButton.IsCancel = true;
            }
            else
            {
                // Input mode — show text entry field and optional hint.
                OkButton.Content = "OK";
                CancelButton.Content = "Cancel";
                OkButton.IsDefault = true;
                CancelButton.IsCancel = true;

                InputTextBox.Visibility = Visibility.Visible;

                if (_inputSpec != null)
                {
                    if (_inputSpec.MaxLength > 0)
                        InputTextBox.MaxLength = _inputSpec.MaxLength;

                    if (!string.IsNullOrWhiteSpace(_inputSpec.Hint))
                    {
                        HintText.Text = _inputSpec.Hint;
                        HintText.Visibility = Visibility.Visible;
                    }
                }

                Loaded += (s, e) => InputTextBox.Focus();
            }
        }

        /// <summary>
        /// Closes the dialog because the backend aborted the prompt.
        /// Sets <see cref="IsAborted"/> so the caller can skip responding.
        /// </summary>
        public void CloseAborted()
        {
            Trace.WriteLine($"{nameof(CloseAborted)}: backend aborted prompt, closing dialog.", GetType().FullName);
            IsAborted = true;
            DialogResult = false;
        }

        private void InputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_inputSpec?.Kind == PromptInputSpecKind.Digits)
                e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private bool ValidateInput()
        {
            if (InputTextBox.Visibility != Visibility.Visible)
                return true;

            string text = InputTextBox.Text;

            if (_inputSpec != null && _inputSpec.MinLength > 0 && text.Length < _inputSpec.MinLength)
            {
                ValidationText.Text = $"Minimum {_inputSpec.MinLength} character(s) required.";
                ValidationText.Visibility = Visibility.Visible;
                return false;
            }

            ValidationText.Visibility = Visibility.Collapsed;
            return true;
        }

        /// <summary>
        /// Updates the displayed prompt message and hint without closing the dialog.
        /// Called when the same prompt ID is re-polled with an updated message.
        /// </summary>
        public void UpdatePrompt(Prompt prompt)
        {
            if (prompt == null)
                return;

            Trace.WriteLine($"{nameof(UpdatePrompt)}: promptId={prompt.PromptId}", GetType().FullName);

            MessageText.Text = prompt.Message;

            if (!string.IsNullOrWhiteSpace(prompt.Input?.Hint))
            {
                HintText.Text = prompt.Input.Hint;
                HintText.Visibility = Visibility.Visible;
            }
        }
    }
}