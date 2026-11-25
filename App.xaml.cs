using System.Windows;

namespace ThereforeLauncher;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { new MainWindow().Show(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Therefore Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
