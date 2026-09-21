using System.Windows;
using System.Windows.Input;

namespace CuadernoDigital.App.Views;

public partial class PromptDialog : Window
{
    private PromptDialog(string title, string question, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        QuestionText.Text = question;
        AnswerBox.Text = defaultValue;
        AnswerBox.SelectAll();
        Loaded += (_, _) => AnswerBox.Focus();
    }

    public string Answer => AnswerBox.Text.Trim();

    public static string? Ask(string title, string question, string defaultValue = "")
    {
        var dialog = new PromptDialog(title, question, defaultValue);
        return dialog.ShowDialog() == true ? dialog.Answer : null;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void AnswerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = true;
        }
    }
}
