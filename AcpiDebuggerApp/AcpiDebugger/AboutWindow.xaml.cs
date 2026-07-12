using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace AcpiDebugger;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        VersionText.Text = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        var executablePath = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "AcpiDebugger.exe");
        BuildDateText.Text = File.GetLastWriteTime(executablePath)
            .ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void Blog_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://ay123.net",
            UseShellExecute = true
        });
    }

}
