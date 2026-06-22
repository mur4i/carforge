using System.Windows;

namespace CarForge.App.Views;

public partial class RenameDialog : Window
{
    public RenameDialog(string oldName)
    {
        InitializeComponent();
        OldNameText.Text = oldName;
        NewNameBox.Text = oldName + "_v2";
        NewNameBox.Focus();
        NewNameBox.SelectAll();
    }

    public string NewName => NewNameBox.Text.Trim();
    public bool RenameHandling => HandlingCheck.IsChecked == true;
    public bool RenameModKit => ModKitCheck.IsChecked == true;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            MessageBox.Show("Digite o novo nome.", "CarForge");
            return;
        }
        DialogResult = true;
    }
}
