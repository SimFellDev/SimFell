using Fellowship.SDK;
using Fellowship.SDK.Client;

namespace FellowshipSim.Codex.Generators;

/// <summary>The game data's textures on the Codex CDN.</summary>
internal static class Textures
{
    /// <summary>The URL of a texture's full size image (<c>https://cdn.codex.fellowshipanalyzer.com/ui/&lt;name&gt;_full.webp</c>).</summary>
    public static string Url(string texture) => CodexAddresses.Texture(texture, TextureSize.Full).AbsoluteUri;
}
