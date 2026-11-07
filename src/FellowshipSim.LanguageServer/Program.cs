using FellowshipSim.LanguageServer;

// `--contributes <package.json> <css folder>` writes the extension's token types and colours; with no arguments, the
// language server runs on stdin and stdout.
if (args is ["--contributes", var packageJson, var cssFolder])
{
    ExtensionContributes.Write(packageJson, cssFolder);
    return 0;
}

var server = new SimcLanguageServer(new LspConnection(Console.OpenStandardInput(), Console.OpenStandardOutput()));
return await server.RunAsync();
