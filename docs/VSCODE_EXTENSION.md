# Edit .simc files in VS Code

The VS Code extension in `src/FellowshipSim.VsCode` colours, checks and completes every line of a `.simc` file: the
action list, the character, the sim's settings and a dungeon. Action lists have the APL Editor's own check and completion
list, and the other lines have the engine's own parsers. It runs SimFell's language server
(`src/FellowshipSim.LanguageServer`) on the user's machine, offline and with no .NET install.

- A profile's hero is its first character line's (`rime="sww"`). A file of action lines alone has the hero of the nearest
  folder in its path named after one, such as `Heroes/Rime/ActionLists` or `data/apls/Rime`. A file of settings or a
  dungeon alone has no hero.
- Each line and comment has its colours, and those of `actions` lines are the editor's.
- A name the hero does not have is a warning. The first `actions` line that does not parse is an error, and so is each
  profile line the engine does not read, such as an unknown key, talent or item, or a pull naming no pull.
- Typing a name or one of `.`, `=`, `/`, `,` or `:`, or Ctrl+Space, opens the completion list. It has the keys the file
  does not have yet, and the values of a key: talents, aspects, sigils, blessings, gems, stat enchants, gear with its ID,
  `true` or `false`, and in a pull its options, the creatures and the other pulls.

Each release has a .vsix for each platform. Package it for one platform (`win32-x64`, `win32-arm64`, `linux-x64`,
`linux-arm64`, `darwin-x64` or `darwin-arm64`), or for every platform with `all`; `--version <x.y.z>` sets the version
of each package:

```
cd src/FellowshipSim.VsCode
npm ci
npm run package -- win32-x64
code --install-extension simfell-simc-win32-x64.vsix
```

`npm run package` publishes the language server self-contained into `server/`, copies the repository's `LICENSE` in as
`LICENSE.txt`, and writes the token types and their
colours into `package.json` from `AplTokenKind` and the `.tok-<kind>` rules of FellowshipSim.UI's CSS. To run the
extension from its folder, build the language server in Debug, run `npm run build`, and start VS Code with
`code --extensionDevelopmentPath=src/FellowshipSim.VsCode`.
