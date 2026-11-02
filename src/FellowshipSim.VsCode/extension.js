// Starts SimFell's language server (FellowshipSim.LanguageServer) for .simc files: the self-contained server packaged in
// server/, or the Debug build of the repository's server when the extension runs from its folder.
const fs = require("fs");
const path = require("path");
const { LanguageClient } = require("vscode-languageclient/node");

let client;

exports.activate = async (context) => {
  const name = "FellowshipSim.LanguageServer";
  const packaged = context.asAbsolutePath(path.join("server", process.platform === "win32" ? `${name}.exe` : name));
  const repository = context.asAbsolutePath(path.join("..", name, "bin", "Debug", "net10.0", `${name}.dll`));
  // A .vsix packed on Windows has no execute permission on the server.
  if (process.platform !== "win32" && fs.existsSync(packaged)) fs.chmodSync(packaged, 0o755);
  const server = fs.existsSync(packaged) ? { command: packaged } : { command: "dotnet", args: [repository] };
  client = new LanguageClient("simfell", "SimFell", server, { documentSelector: [{ language: "simc" }] });
  await client.start();
};

exports.deactivate = () => client?.stop();
