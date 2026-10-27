namespace FellowshipSim.Desktop;

public sealed class App(MainPage page) : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(page) { Title = "SimFell", Width = 1280, Height = 900 };
}
