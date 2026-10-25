namespace FellowshipSim.Desktop;

public sealed class App(MainPage page) : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(page) { Title = "FellowshipSim", Width = 1280, Height = 900 };
}
