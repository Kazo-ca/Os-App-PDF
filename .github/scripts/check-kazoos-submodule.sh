#!/usr/bin/env bash
# ADR 0176 §6: the Kazo OS submodule is never modified in an app repository, and it sits exactly
# on the release tag of the version it declares (build/Versions.props). A local change of the OS
# can therefore never reach a published build.
#
# Kazo Pdf, until the Kazo OS release that brings ADR 0264 is tagged and published (doc/en/plan.md,
# decisions 2 and 10): with KAZOOS_BRANCH set, the submodule may instead sit on a commit of that
# branch of Kazo-ca/Os. CI then builds the source profile, and nothing is
# published: the Store waits for the release (work item 4 removes KAZOOS_BRANCH).
set -euo pipefail

submodule="external/KazoOs"
versions="$submodule/wasm2/KazoOs/build/Versions.props"
if [[ ! -f "$versions" ]]; then
  echo "::error::$versions not found. Run: git submodule update --init"
  exit 1
fi

version="$(sed -n 's:.*<KazoOsVersion>\(.*\)</KazoOsVersion>.*:\1:p' "$versions")"
if [[ -n "$(git -C "$submodule" status --porcelain)" ]]; then
  echo "::error::$submodule has local changes. The OS is not modified from an app repository."
  git -C "$submodule" status --short
  exit 1
fi

head="$(git -C "$submodule" rev-parse HEAD)"
if [[ -n "${KAZOOS_BRANCH:-}" ]]; then
  git -C "$submodule" fetch --quiet --depth 200 origin "refs/heads/$KAZOOS_BRANCH"
  branch="$(git -C "$submodule" rev-parse FETCH_HEAD)"
  if [[ "$head" != "$branch" ]] && ! git -C "$submodule" merge-base --is-ancestor "$head" "$branch"; then
    echo "::error::$submodule is at $head, which is not on $KAZOOS_BRANCH ($branch)."
    exit 1
  fi
  echo "Kazo OS submodule: $head on $KAZOOS_BRANCH (pre-release module), clean."
  exit 0
fi

git -C "$submodule" fetch --quiet --depth 1 origin "refs/tags/v$version:refs/tags/v$version"
tag="$(git -C "$submodule" rev-parse "v$version^{commit}")"
if [[ "$head" != "$tag" ]]; then
  echo "::error::$submodule is at $head, not on the tag v$version ($tag). Move it with: git -C $submodule checkout v$version"
  exit 1
fi
echo "Kazo OS submodule: v$version ($head), clean."
