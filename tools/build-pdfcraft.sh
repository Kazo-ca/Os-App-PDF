#!/usr/bin/env bash
# Compiles PdfCraft from the external/pdfcraft submodule (our fork Kazo-ca/pdfcraft, work item 2 of
# doc/en/plan.md) the way its release workflow builds the web zip, then vendors the result in
# src/Kazo.Pdf/wwwroot/pdfcraft/ with tools/vendor-pdfcraft.sh --dir. The
# .github/workflows/build-pdfcraft.yml workflow runs it; it also runs on a developer machine.
#
# Usage:
#   tools/build-pdfcraft.sh
#
# Needs: Rust stable (1.95 or later) with the wasm32-unknown-unknown target, Trunk at the version
# below, git, zip, unzip and network access (craft-i18n, and the OCR models that PdfCraft's
# `cargo xtask models` downloads, pinned by SHA-256). PdfCraft's web release embeds the
# translations of storytold/craft-i18n at the commit of its craft-i18n.rev, and no craft-fonts
# (its release workflow does not set web-craft-fonts): this build does the same. CRAFT_I18N_DIR,
# when set, names a craft-i18n checkout to use instead of cloning one; it must be on that commit.
set -euo pipefail

# The Trunk version of PdfCraft's release workflow (craft-common-actions, install-trunk).
trunk_version="0.21.14"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pdfcraft="$repo_root/external/pdfcraft"

case "${1:-}" in
  "") ;;
  -h|--help) sed -n '2,15p' "$0"; exit 0 ;;
  *) echo "Unknown argument: $1" >&2; exit 2 ;;
esac

fail() { echo "error: $*" >&2; exit 1; }

[[ -f "$pdfcraft/Cargo.toml" ]] || fail "external/pdfcraft is empty. Run: git submodule update --init external/pdfcraft"
# A local change of PdfCraft must go through a commit of the fork, so the provenance names it.
[[ -z "$(git -C "$pdfcraft" status --porcelain)" ]] || fail "external/pdfcraft has local changes; commit them in the fork first"

command -v trunk >/dev/null || fail "trunk not found; install Trunk $trunk_version"
actual_trunk="$(trunk --version | awk '{print $2}')"
[[ "$actual_trunk" == "$trunk_version" ]] || fail "trunk $actual_trunk found; PdfCraft's release uses $trunk_version"
rustup target list --installed 2>/dev/null | grep -qx wasm32-unknown-unknown \
  || fail "the wasm32-unknown-unknown target is missing (rustup target add wasm32-unknown-unknown)"
command -v zip >/dev/null && command -v unzip >/dev/null || fail "zip and unzip are needed"

commit="$(git -C "$pdfcraft" rev-parse HEAD)"
rustc_version="$(rustc --version)"
i18n_rev="$(tr -d '[:space:]' < "$pdfcraft/craft-i18n.rev")"
[[ "$i18n_rev" =~ ^[0-9a-f]{40}$ ]] || fail "external/pdfcraft/craft-i18n.rev does not hold one commit SHA"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# craft-i18n at PdfCraft's pin: the release embeds its catalogs, and without them the build has
# English only.
i18n="${CRAFT_I18N_DIR:-}"
if [[ -z "$i18n" ]]; then
  i18n="$work/craft-i18n"
  git init -q "$i18n"
  git -C "$i18n" fetch -q --depth 1 https://github.com/storytold/craft-i18n.git "$i18n_rev"
  git -C "$i18n" checkout -q FETCH_HEAD
fi
[[ "$(git -C "$i18n" rev-parse HEAD)" == "$i18n_rev" ]] || fail "$i18n is not on craft-i18n $i18n_rev"

# PdfCraft's own packaging script: trunk build --release, the OCR models, then the licences and
# NOTICE next to the site. The build date is the commit's (UTC), so building the same commit twice
# bakes the same date into the About window. CRAFT_FONTS_DIR is cleared so a value left in the
# developer's shell does not embed fonts the official web zip does not have.
build_date="$(TZ=UTC git -C "$pdfcraft" log -1 --format=%cd --date=format-local:%Y-%m-%d)"
echo "Building PdfCraft $commit (rustc: $rustc_version, trunk $actual_trunk, craft-i18n $i18n_rev)"
(
  unset CRAFT_FONTS_DIR CRAFT_FONTS_REQUIRED PDFCRAFT_BUILD_LABEL PDFCRAFT_VERSION
  export CRAFT_I18N_DIR="$i18n" CRAFT_I18N_REQUIRED=1
  export PDFCRAFT_BUILD_SHA="$commit" PDFCRAFT_BUILD_DATE="$build_date"
  export DIST="$work/dist"
  export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-$pdfcraft/target}"
  cd "$pdfcraft"
  packaging/web/package.sh
)

shopt -s nullglob
zips=("$work"/dist/pdfcraft-web-*.zip)
shopt -u nullglob
[[ ${#zips[@]} -eq 1 ]] || fail "packaging/web/package.sh made no single pdfcraft-web-*.zip"
unzip -q "${zips[0]}" -d "$work/site"
site=("$work"/site/pdfcraft-web-*)
[[ ${#site[@]} -eq 1 && -d "${site[0]}" ]] || fail "the zip has no single pdfcraft-web-*/ folder"

"$repo_root/tools/vendor-pdfcraft.sh" --dir "${site[0]}" \
  --craft-i18n "$i18n_rev" --rustc "$rustc_version" --trunk "$actual_trunk"
