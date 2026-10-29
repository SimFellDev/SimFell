#!/usr/bin/env bash
# Publishes the browser build (FellowshipSim.Web) to the Quarto GitHub Pages repo, in one go:
# AOT publish -> copy into <site>/simfell and <site>/docs/simfell -> commit -> push.
#
#   tools/publish-pages.sh            publish, amend the SimFell commit (or make it the first time), push
#   tools/publish-pages.sh --no-push  everything but the push
#
# Environment: SITE_REPO (default ../../Textspace/maxiqe.github.io from this repo), SITE_PATH (default simfell),
# DOTNET (default dotnet; needs the wasm-tools workload: `sudo dotnet workload install wasm-tools`).
#
# The history stays short: if the site repo's last commit is the SimFell one, it is amended (and force-pushed, with a
# lease) instead of adding a commit that holds another ~22 MB copy of the app. Any other last commit is left alone and a
# new one is made on top.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
site="$(cd "${SITE_REPO:-$repo/../../Textspace/maxiqe.github.io}" && pwd)"
path="${SITE_PATH:-simfell}"
dotnet="${DOTNET:-dotnet}"
message="SimFell preview"
push=1
[[ "${1:-}" == "--no-push" ]] && push=0

"$dotnet" workload list 2>/dev/null | grep -q wasm-tools ||
  { echo "wasm-tools is not installed for '$dotnet' (sudo dotnet workload install wasm-tools): the workers would run 4x slower." >&2; exit 1; }
[[ -z "$(git -C "$site" diff --cached --name-only)" ]] ||
  { echo "The site repo has staged changes; commit or unstage them first." >&2; exit 1; }

out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

echo "== publishing (AOT, Release)"
"$dotnet" publish "$repo/src/FellowshipSim.Web/FellowshipSim.Web.csproj" -c Release -p:SimWorkerAot=true -o "$out" --nologo -v:q

echo "== copying into $site/$path and $site/docs/$path"
# docs/ is what Pages serves; the top-level copy is the source Quarto copies there on a render (project.resources).
for dest in "$site/$path" "$site/docs/$path"; do
  rm -rf "$dest"
  mkdir -p "$dest"
  cp -R "$out/wwwroot/." "$dest/"
  # Pages compresses what it serves; the .br and .gz copies are dead weight.
  find "$dest" \( -name '*.br' -o -name '*.gz' \) -delete
  sed -i.bak "s#<base href=\"/\" */>#<base href=\"/$path/\" />#" "$dest/index.html"
  rm "$dest/index.html.bak"
  grep -q "<base href=\"/$path/\"" "$dest/index.html" || { echo "base href not set in $dest/index.html" >&2; exit 1; }
done

cd "$site"
git add -- "$path" "docs/$path" _quarto.yml
if git diff --cached --quiet; then echo "== nothing changed"; exit 0; fi

amend=0
[[ "$(git log -1 --format=%s)" == "$message" ]] && amend=1
if (( amend )); then
  echo "== amending '$message'"
  git commit --amend --no-edit --quiet
else
  echo "== committing '$message'"
  git commit --quiet -m "$message"
fi

if (( push )); then
  echo "== pushing"
  if (( amend )); then git push --force-with-lease; else git push; fi
fi
echo "== done: https://$(basename "$site" | sed 's/\.github\.io$//').github.io/$path/"
