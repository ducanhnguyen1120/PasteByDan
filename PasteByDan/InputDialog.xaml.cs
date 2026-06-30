using System.Windows;

namespace PasteByDan
{
    public partial class InputDialog : Window
    {
        public string Result { get; private set; }

        public InputDialog(string title, string prompt)
        {
            InitializeComponent();
            Title = title;
            PromptLabel.Text = prompt;
            InputBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Result = InputBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
