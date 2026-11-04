#!/usr/bin/env bash
# Downloads the Codex's data export (entities.jsonl and settings.json, of the build the Codex reads) into codex-export/,
# where FellowshipSim.Core's build reads it. Run it once after cloning, and again after a new datamine.
#
#     tools/download-codex-export.sh
set -euo pipefail

codex="https://codex.fellowshipanalyzer.com/api/export"
out="$(cd "$(dirname "$0")/.." && pwd)/codex-export"
mkdir -p "$out"
for file in entities.jsonl settings.json; do
  curl --fail --silent --show-error --location --user-agent "FellowshipSim/1.0" -o "$out/$file.part" "$codex/$file"
  mv "$out/$file.part" "$out/$file"
done
curl --fail --silent --show-error --user-agent "FellowshipSim/1.0" -o "$out/export.json" "$codex"
echo "Codex export $(sed -E 's/.*"build":"([^"]*)".*/\1/' "$out/export.json") in $out"
