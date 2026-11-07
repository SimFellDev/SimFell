// Packages the extension: `npm run package -- [<target>|all] [--version <x.y.z>]` (win32-x64 when no target is given;
// package.json's version when none is given). Each target's language server is published self-contained for its runtime
// into server/, so the extension needs no .NET install and no network.
import { execFileSync } from "node:child_process";
import { rmSync } from "node:fs";
import { parseArgs } from "node:util";

const runtimes = {
  "win32-x64": "win-x64",
  "win32-arm64": "win-arm64",
  "linux-x64": "linux-x64",
  "linux-arm64": "linux-arm64",
  "darwin-x64": "osx-x64",
  "darwin-arm64": "osx-arm64",
};
const { values, positionals } = parseArgs({ options: { version: { type: "string" } }, allowPositionals: true });
const asked = positionals[0] ?? "win32-x64";
const targets = asked === "all" ? Object.keys(runtimes) : [asked];
for (const target of targets) {
  if (!runtimes[target]) throw new Error(`No runtime for ${target}; the targets are ${Object.keys(runtimes).join(", ")} or all.`);
}

const run = (command, args) => execFileSync(command, args, { stdio: "inherit", shell: process.platform === "win32" });
run("dotnet", ["run", "--project", "../FellowshipSim.LanguageServer", "-c", "Release", "--", "--contributes", "package.json", "../FellowshipSim.UI/wwwroot/css"]);
run("npm", ["run", "build"]);
for (const target of targets) {
  rmSync("server", { recursive: true, force: true });
  run("dotnet", ["publish", "../FellowshipSim.LanguageServer", "-c", "Release", "-r", runtimes[target], "--self-contained",
    "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true", "-p:DebugType=none", "-o", "server"]);
  const version = values.version ? [values.version, "--no-git-tag-version", "--no-update-package-json"] : [];
  run("npx", ["vsce", "package", ...version, "--no-dependencies", "--target", target, "--out", `simfell-simc-${target}.vsix`]);
}
