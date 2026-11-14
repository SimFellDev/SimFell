#!/usr/bin/env bash
# Checks that the committed src/FellowshipSim.Core/Generated/Codex/ is what the Codex generators write. It writes the folder
# again from codex-export/ (tools/regenerate-codex.sh) and fails when git shows a change to the folder. A Codex export other
# than the one in the folder's codex-export.sha256 gives other files, so then the check gives a warning and passes.
#
#     tools/download-codex-export.sh && tools/check-codex-generated.sh
set -euo pipefail

cd "$(dirname "$0")/.."
folder=src/FellowshipSim.Core/Generated/Codex

summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
      echo "### $1"
      if [ -n "${2:-}" ]; then
        echo '```'
        echo "$2"
        echo '```'
      fi
    } >> "$GITHUB_STEP_SUMMARY"
  fi
}

# The export's hash, in the form of codex-export.sha256.
exported=$(for file in entities.jsonl settings.json; do
  echo "$(sha256sum < "codex-export/$file" | cut -d' ' -f1)  $file"
done)
committed=$(tr -d '\r' < "$folder/codex-export.sha256")
if [ "$exported" != "$committed" ]; then
  message="The Codex export differs from the one the committed generated files are from, so they are not checked. Run tools/download-codex-export.sh and tools/regenerate-codex.sh, and commit $folder."
  echo "::warning::$message"
  echo "Committed: $committed"
  echo "Exported:  $exported"
  summary "$message"
  exit 0
fi

tools/regenerate-codex.sh
changes=$(git status --porcelain -- "$folder")
if [ -z "$changes" ]; then
  echo "The Codex generators' files match the committed files."
  exit 0
fi
message="The Codex generators' files differ from the committed files. Run tools/regenerate-codex.sh and commit $folder."
echo "::error::$message"
echo "$changes"
git --no-pager diff --stat -- "$folder"
summary "$message" "$changes"
exit 1
