#!/usr/bin/env bash
# Has Ollama shipped a release or changed its API spec since the snapshot in this folder?
#   check.sh            open a GitHub issue when something changed (needs gh and GH_TOKEN)
#   check.sh --dry-run  print the issue instead of opening it
#   check.sh --refresh  record the current release and spec as reviewed
set -euo pipefail

mode="${1:-}"
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

release_url="https://api.github.com/repos/ollama/ollama/releases/latest"
spec_url="https://docs.ollama.com/openapi.yaml"

if [[ -n "${GH_TOKEN:-}" ]]; then
  curl -fsSL -H "Authorization: Bearer $GH_TOKEN" "$release_url" > "$work/release.json"
else
  curl -fsSL "$release_url" > "$work/release.json"
fi
curl -fsSL "$spec_url" > "$work/openapi.yaml"

tag="$(jq -r .tag_name "$work/release.json")"
known="$(tr -d '[:space:]' < "$here/release.txt")"

if [[ "$mode" == "--refresh" ]]; then
  echo "$tag" > "$here/release.txt"
  cp "$work/openapi.yaml" "$here/openapi.yaml"
  echo "Snapshot now at Ollama $tag."
  exit 0
fi

spec_changed=false
diff -u --label reviewed --label current "$here/openapi.yaml" "$work/openapi.yaml" > "$work/spec.diff" || spec_changed=true

if [[ "$tag" == "$known" && "$spec_changed" == false ]]; then
  echo "Up to date: Ollama $known, API spec unchanged."
  exit 0
fi

if [[ "$tag" != "$known" ]]; then
  title="Ollama $tag: review API changes"
  intro="Ollama **$tag** is out (last reviewed: **$known**)."
  notes="$(jq -r '.body // ""' "$work/release.json" | head -c 12000)"
else
  title="Ollama API spec changed after $known"
  intro="The published API spec changed while Ollama is still at **$known**."
  notes="No new release; only the published spec changed."
fi

spec_section="Unchanged."
if [[ "$spec_changed" == true ]]; then
  spec_section="$(printf '```diff\n%s\n```' "$(head -c 45000 "$work/spec.diff")")"
fi

cat > "$work/issue.md" <<BODY
$intro Check whether Snail.Toolkit.AI.Ollama needs changes — new fields, endpoints or capabilities — then record the review:

    .github/ollama/check.sh --refresh

## Release notes

$notes

## API spec diff ($spec_url)

$spec_section
BODY

if [[ "$mode" == "--dry-run" ]]; then
  echo "# $title"
  cat "$work/issue.md"
  exit 0
fi

open="$(gh issue list --state open --search "\"$title\" in:title" --json number --jq 'length')"
if [[ "$open" != "0" ]]; then
  echo "Issue already open: $title"
  exit 0
fi

gh issue create --title "$title" --body-file "$work/issue.md"
