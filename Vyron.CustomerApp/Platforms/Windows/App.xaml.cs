using Microsoft.UI.Xaml;

namespace Vyron.CustomerApp.WinUI;

public partial class App : MauiWinUIApplication
{
    public App() { InitializeComponent(); }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
