namespace Wavy.Android;

public partial class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Title = "Wavy";
        window.Page  = new AppShell();
        return window;
    }
}
