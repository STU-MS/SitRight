using System;
using System.Windows;
namespace SitRight;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Log("应用程序已启动");
    }

    protected void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        });
    }

    protected void UpdateStatus(string status, string color = "Gray")
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }
}
