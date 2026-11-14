#!/usr/bin/env bash
# Writes src/FellowshipSim.Core/Generated/Codex/ again from the Codex export in codex-export/ (tools/download-codex-export.sh):
# every Codex generator's files, and codex-export.sha256, the hash of the export they are from. Run it after a new export,
# a change to a generator, or a new or renamed partial class the generators write a half of. Commit the folder after it.
#
#     tools/regenerate-codex.sh
set -euo pipefail

cd "$(dirname "$0")/.."
# --no-incremental: an up-to-date build compiles nothing, so the generators would write no file.
dotnet build src/FellowshipSim.Core -c Release --no-incremental -p:RegenerateCodex=true -nologo -v:q
