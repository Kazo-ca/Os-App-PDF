# Third-party notices

> [Version française](THIRD-PARTY-NOTICES-fr.md)

Kazo Pdf is under the MIT license ([LICENSE](LICENSE)). Its C# code, its iframe page
(`src/Kazo.Pdf/wwwroot/host/`) and its icon (`src/Kazo.Pdf/wwwroot/icons/app.svg`,
`store/icon.png`) are this repository's own work. The iframe proxy it uses (`craft-proxy.js`)
belongs to Kazo OS (`Kazo.Os.AppSdk.Craft`, MIT).

It bundles one third-party work, **PdfCraft**, which brings its own third-party material,
including the text recognition models.

## PdfCraft

| | |
| --- | --- |
| Work | [PdfCraft](https://github.com/storytold/pdfcraft), a PDF editor written in Rust, by the ArtCraft team and the PdfCraft contributors |
| Version | 0.21.1, compiled from our fork [`Kazo-ca/pdfcraft`](https://github.com/Kazo-ca/pdfcraft) (branch `kazo/v0.21.1`, the upstream tag, unmodified) |
| License | MIT or Apache-2.0, at the user's option |
| Files | `src/Kazo.Pdf/wwwroot/pdfcraft/` (the web build: `.js`, `.wasm`, `snippets/`, the OCR worker, `models/`) |
| Provenance | `src/Kazo.Pdf/wwwroot/pdfcraft/pdfcraft.json`: the fork's commit, the compilers, and the SHA-256 of every file |

Kazo Pdf does not change PdfCraft: it shows its web build in an iframe and talks to it through the
control commands and events PdfCraft publishes for a host page.

PdfCraft's own notices ship **unmodified** next to its build, and in the installed package
(`_content/Kazo.Pdf/pdfcraft/`):

| File | Content |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | PdfCraft's licenses |
| `NOTICE` | PdfCraft's copyright, its trademark notice, and the third-party material it bundles, with the notices those licenses require |
| `models/ATTRIBUTION.txt`, `models/*.LICENCE.txt` | The text recognition models and their license (below) |

### Files the NOTICE refers to

PdfCraft's `NOTICE` names license files of its source repository that its web release does not
carry. Kazo Pdf ships them, copied unmodified from the fork at the bundled commit
(`b503b2e62dfeec5598f344e4e287d9c9cbdb34fa`), in `src/Kazo.Pdf/wwwroot/legal/pdfcraft/`:

| File | Source in PdfCraft | Covers |
| --- | --- | --- |
| `ATTRIBUTION.md` | `ATTRIBUTION.md` | PdfCraft's per-file list of assets (author, source, license) |
| `OFL-Inter.txt`, `OFL-JetBrainsMono.txt`, `OFL-DancingScript.txt` | `assets/fonts/` | The Inter, JetBrains Mono and Dancing Script typefaces (SIL Open Font License 1.1) |
| `LICENSE-lucide.txt` | `assets/icons/` | The Lucide icons (ISC and MIT) |
| `egui-OFL.txt`, `egui-UFL.txt`, `egui-Hack-Regular.txt`, `egui-emoji-icon-font-mit-license.txt` | `licenses/` | The egui default fonts: Noto Emoji (OFL 1.1), Ubuntu Light (Ubuntu Font Licence 1.0), Hack (MIT and Bitstream Vera), emoji-icon-font (MIT) |
| `ICU4X-LICENSE.txt` | `licenses/` | The ICU4X line-breaking data (Unicode-3.0) |
| `CMap-LICENSE.txt` | `licenses/` | The CMap resources bundled by hayro-cmap (BSD-3-Clause) |
| `prc-rs-LICENSE.txt` | `licenses/` | prc-rs |
| `LICENSE_FOXIT` | `vendor/hayro-interpret/assets/` | The Foxit standard-14 substitute fonts (BSD-3-Clause) |
| `edit-session-LICENSE-MIT`, `edit-session-LICENSE-APACHE` | `vendor/edit-session/` | edit-session |

PdfCraft's web release embeds no craft-fonts font.

### Text recognition models

| | |
| --- | --- |
| Work | The Ocrs text detection and text recognition models (`text-detection.onnx`, `text-recognition.onnx`), by Robert Knight, trained on HierText |
| Source | [ocrs](https://github.com/robertknight/ocrs) (author-published ONNX models), downloaded and checked by SHA-256 by PdfCraft's `cargo xtask models` |
| License | [CC-BY-SA-4.0](https://creativecommons.org/licenses/by-sa/4.0/legalcode), full text in `models/*.LICENCE.txt` |
| Changes | None: the models ship unmodified |

### The ArtCraft brand

The ArtCraft name, wordmark and logos are trademarks of the ArtCraft team; PdfCraft's open-source
license does not cover them. Kazo Pdf uses none of them: its name, its icon and its Store listing
are this repository's own. "Based on PdfCraft by the ArtCraft team" only says where the software
comes from.

## In the package

The Kazo OS package (`.kazoapp`) carries, under `wwwroot/`:

- `legal/LICENSE.txt`: Kazo Pdf's MIT license (this repository's `LICENSE`);
- `legal/THIRD-PARTY-NOTICES.md` and `legal/THIRD-PARTY-NOTICES-fr.md`: this file and its French
  version;
- `legal/pdfcraft/`: the license files PdfCraft's `NOTICE` refers to, as above;
- `pdfcraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `models/ATTRIBUTION.txt` and
  `models/*.LICENCE.txt`: PdfCraft's notices and the models' license, as above.

A C# test (`LegalFilesTests`) checks that the project puts these files in the package.

## Test data

The end-to-end tests make their own documents with Kazo Pdf; they ship no third-party file.
