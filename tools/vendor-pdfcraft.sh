#!/usr/bin/env bash
# Puts a PdfCraft web build in src/Kazo.Pdf/wwwroot/pdfcraft/ (decision 5 of doc/en/plan.md; the
# same method as Kazo Spreadsheets' vendor-gridcraft.sh). That folder is never edited by hand: this
# script replaces it whole, writes pdfcraft.json, its provenance (version, source, name, size and
# SHA-256 of each file), and names the OCR worker in the iframe page (src/Kazo.Pdf/wwwroot/host/index.html,
# <meta name="pdfcraft-ocr-worker">). tests/Kazo.Pdf.Tests checks the files still match it.
#
# Usage:
#   tools/vendor-pdfcraft.sh                 download the pinned official zip and check it
#   tools/vendor-pdfcraft.sh --zip <file>    use a zip already downloaded (still checked)
#   tools/vendor-pdfcraft.sh --dir <folder>  use our own build of external/pdfcraft (work item 2)
#
# --dir takes a folder laid out like the zip's pdfcraft-web-<version>/, as PdfCraft's
# packaging/web/package.sh makes it from the external/pdfcraft submodule. The provenance then
# names the fork and the submodule's commit instead of the zip; --craft-i18n, --craft-fonts,
# --rustc and --trunk add what the build used.
set -euo pipefail

# The pinned release. Moving to another version changes these three lines, in a PR.
version="0.21.1"
zip_url="https://github.com/storytold/pdfcraft/releases/download/v${version}/pdfcraft-web-${version}.zip"
zip_sha256="fe9708d46af191aebb56f70eb32cf8b112b0b9452d17ae6ba3a8c4a8665fe748"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="$repo_root/src/Kazo.Pdf/wwwroot/pdfcraft"
host_page="$repo_root/src/Kazo.Pdf/wwwroot/host/index.html"
submodule="$repo_root/external/pdfcraft"

fail() { echo "$*" >&2; exit 1; }

zip_file=""
build_dir=""
i18n_rev=""
fonts_rev=""
rustc_version=""
trunk_version=""
while [[ $# -gt 0 ]]; do
  [[ $# -ge 2 || "$1" == -h || "$1" == --help ]] || fail "$1 needs a value"
  case "$1" in
    --zip) zip_file="$2"; shift 2 ;;
    --dir) build_dir="$2"; shift 2 ;;
    --craft-i18n) i18n_rev="$2"; shift 2 ;;
    --craft-fonts) fonts_rev="$2"; shift 2 ;;
    --rustc) rustc_version="$2"; shift 2 ;;
    --trunk) trunk_version="$2"; shift 2 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -z "$zip_file" || -z "$build_dir" ]] || fail "--zip and --dir are two different sources; give one"
[[ -f "$host_page" ]] || fail "${host_page#"$repo_root"/} is missing: the worker is named there"

# Escapes a value for a JSON string (the rustc version is free text).
json_string() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '"%s"' "$value"
}

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

if [[ -n "$build_dir" ]]; then
  # Our build: the provenance names the fork and the submodule's commit, which must be clean so
  # the commit alone says what was compiled.
  [[ -d "$build_dir" ]] || fail "$build_dir is not a folder"
  source_dir="$(cd "$build_dir" && pwd)"
  [[ -f "$submodule/Cargo.toml" ]] || fail "external/pdfcraft is empty. Run: git submodule update --init external/pdfcraft"
  [[ -z "$(git -C "$submodule" status --porcelain)" ]] || fail "external/pdfcraft has local changes; the provenance could not name what was built"
  commit="$(git -C "$submodule" rev-parse HEAD)"
  repository="$(git -C "$repo_root" config -f .gitmodules submodule.external/pdfcraft.url)"
  version="$(awk '/^\[/ { in_pkg = ($0 == "[workspace.package]"); next }
                  in_pkg && $1 == "version" { gsub(/[" ]/, "", $3); print $3; exit }' "$submodule/Cargo.toml")"
  [[ -n "$version" ]] || fail "no [workspace.package] version in external/pdfcraft/Cargo.toml"
else
  if [[ -z "$zip_file" ]]; then
    zip_file="$work/pdfcraft-web-${version}.zip"
    echo "Downloading $zip_url"
    curl --fail --location --silent --show-error --retry 3 -o "$zip_file" "$zip_url"
  fi

  actual="$(sha256sum "$zip_file" | cut -d' ' -f1)"
  [[ "$actual" == "$zip_sha256" ]] || fail "SHA-256 mismatch for $zip_file: expected $zip_sha256, got $actual"

  unzip -q "$zip_file" -d "$work/zip"
  source_dir="$work/zip/pdfcraft-web-${version}"
  [[ -d "$source_dir" ]] || fail "The zip has no pdfcraft-web-${version}/ folder"
fi

# Exactly one glue module and one .wasm for the app, and one worker with its loader, named by
# Trunk with their hashes.
shopt -s nullglob
glues=("$source_dir"/pdfcraft-web-*.js)
wasms=("$source_dir"/pdfcraft-web-*_bg.wasm)
loaders=("$source_dir"/pdfcraft-ocr-worker-*_loader.js)
shopt -u nullglob
if [[ ${#glues[@]} -ne 1 || ${#wasms[@]} -ne 1 ]]; then
  fail "Expected one pdfcraft-web-*.js and one pdfcraft-web-*_bg.wasm in $source_dir"
fi
[[ ${#loaders[@]} -eq 1 ]] || fail "Expected one pdfcraft-ocr-worker-*_loader.js in $source_dir"
glue="$(basename "${glues[0]}")"
wasm="$(basename "${wasms[0]}")"
loader="$(basename "${loaders[0]}")"
worker="${loader%_loader.js}"
for needed in snippets LICENSE-MIT LICENSE-APACHE NOTICE "$worker.js" "${worker}_bg.wasm" \
              models/ATTRIBUTION.txt models/text-detection.onnx models/text-recognition.onnx; do
  [[ -e "$source_dir/$needed" ]] || fail "$needed is missing from $source_dir (make it with packaging/web/package.sh)"
done

# What the app needs: the glue, the .wasm, the snippets the glue imports, the OCR worker (loader,
# glue, .wasm) and its models with their licences (Q2 = A), and every licence and notice. Left out:
# PdfCraft's own page (host/index.html replaces it), its README and hosting notes, and the sample
# server rules (_headers, .htaccess).
rm -rf "$target"
mkdir -p "$target/models"
cp "$source_dir/$glue" "$source_dir/$wasm" "$target/"
cp "$source_dir/$loader" "$source_dir/$worker.js" "$source_dir/${worker}_bg.wasm" "$target/"
cp -R "$source_dir/snippets" "$target/"
cp "$source_dir"/models/* "$target/models/"
cp "$source_dir"/LICENSE-* "$source_dir/NOTICE" "$target/"
shopt -s nullglob
font_licences=("$source_dir"/OFL-*.txt)
shopt -u nullglob
[[ ${#font_licences[@]} -eq 0 ]] || cp "${font_licences[@]}" "$target/"

# The page names the worker's loader, relative to the build folder (the page's <base>).
sed -i -E "s#(<meta name=\"pdfcraft-ocr-worker\" content=\")[^\"]*(\")#\1${loader}\2#" "$host_page"
grep -q "content=\"${loader}\"" "$host_page" || fail "${host_page#"$repo_root"/} has no <meta name=\"pdfcraft-ocr-worker\"> to name the worker"

# The provenance file, files sorted by path so a new version gives a readable diff.
{
  printf '{\n'
  printf '  "version": "%s",\n' "$version"
  printf '  "source": {\n'
  if [[ -n "$build_dir" ]]; then
    printf '    "kind": "source-build",\n'
    printf '    "repository": %s,\n' "$(json_string "$repository")"
    printf '    "commit": "%s"' "$commit"
    [[ -z "$i18n_rev" ]] || printf ',\n    "craftI18n": "%s"' "$i18n_rev"
    [[ -z "$fonts_rev" ]] || printf ',\n    "craftFonts": "%s"' "$fonts_rev"
    [[ -z "$rustc_version" ]] || printf ',\n    "rustc": %s' "$(json_string "$rustc_version")"
    [[ -z "$trunk_version" ]] || printf ',\n    "trunk": %s' "$(json_string "$trunk_version")"
    printf '\n'
  else
    printf '    "kind": "release-zip",\n'
    printf '    "url": "%s",\n' "$zip_url"
    printf '    "sha256": "%s"\n' "$zip_sha256"
  fi
  printf '  },\n'
  printf '  "glue": "%s",\n' "$glue"
  printf '  "wasm": "%s",\n' "$wasm"
  printf '  "worker": "%s",\n' "$loader"
  printf '  "files": [\n'
  first=1
  while IFS= read -r path; do
    size="$(stat -c %s "$target/$path")"
    sha="$(sha256sum "$target/$path" | cut -d' ' -f1)"
    [[ $first -eq 1 ]] || printf ',\n'
    first=0
    printf '    { "path": "%s", "size": %s, "sha256": "%s" }' "$path" "$size" "$sha"
  done < <(cd "$target" && find . -type f ! -path ./pdfcraft.json | sed 's#^\./##' | LC_ALL=C sort)
  printf '\n  ]\n'
  printf '}\n'
} > "$target/pdfcraft.json"

echo "PdfCraft $version vendored in ${target#"$repo_root"/}: $glue, $wasm, worker $loader"
