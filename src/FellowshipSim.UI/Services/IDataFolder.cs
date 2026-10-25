namespace FellowshipSim.UI.Services;

/// <summary>
/// Opens the data folder in the computer's file manager, for an app that runs on its user's own computer (the desktop app,
/// the one-person web app). The pages offer this, never the folder's path: the path names the user, and a streamer's
/// screen would show it. A host that serves other people's browsers registers none.
/// </summary>
public interface IDataFolder
{
    Task OpenAsync();
}
