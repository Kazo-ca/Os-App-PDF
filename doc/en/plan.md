> [Version française](../fr/plan.md)

# Plan: the Kazo Pdf app for Kazo OS

Step 0 of the Kazo OS [Craft app port guide](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/craft-app-port-guide.md),
written on 2026-11-13 (`TODO` issue [#1](https://github.com/Kazo-ca/Os-App-PDF/issues/1)).
This document sets the goal, what reading PdfCraft showed, the decisions, the questions asked to
Benoit and the order of the work items. The method comes from Kazo Word and Kazo Spreadsheets
([plan](https://github.com/Kazo-ca/Os-App-Spreadsheets/blob/spreadsheets/base/doc/en/plan.md)),
the closest reference.

## Goal

Kazo Pdf is the **PDF reader and editor of Kazo OS**. It wraps the web build of
[PdfCraft](https://github.com/storytold/pdfcraft) (ArtCraft team, `MIT OR Apache-2.0`), written in
Rust (egui), in alpha, and plugs its host mode (`craft-web-host`) into the files, dialogs and
associations of Kazo OS. Nothing of PdfCraft is rewritten.

The Kazo code shared by the Craft apps (iframe, proxy, file picker, shortcuts, settings,
`DocumentSession`, save rule) comes from the Kazo OS module `Kazo.Os.AppSdk.Craft`
([ADR 0259](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0259-shared-craft-app-host-module.md)),
shipped with **Kazo OS 3.3.3**. Kazo Pdf keeps only its window, its format list, its iframe page,
the PdfCraft build and its manifest.

Today **no Kazo OS app opens `.pdf` files**: the OS browser shows a PDF received over HTTP, but no
app is associated with the extension. Kazo Pdf becomes the default app for it.

## Reading PdfCraft (step 0, 2026-11-13)

Read on the upstream tag `v0.21.1` (commit `b503b2e6`, 2026-11-11; it is not on `main` of the
fork [`Kazo-ca/pdfcraft`](https://github.com/Kazo-ca/pdfcraft), as for GridCraft) and on the
official zip `pdfcraft-web-0.21.1.zip`.

| Point | Finding |
| --- | --- |
| Web build | `apps/pdfcraft-web` (Trunk, `public_url = "./"`), depends on `craft-web-host` and `craft-web-api` at `craft-libs` revision `fd5e2fe`. Rust 1.95 at least. `craft-i18n` languages built into the `.wasm` (revision `575cfde`). |
| Official zip | `https://github.com/storytold/pdfcraft/releases/download/v0.21.1/pdfcraft-web-0.21.1.zip`, SHA-256 `fe9708d46af191aebb56f70eb32cf8b112b0b9452d17ae6ba3a8c4a8665fe748`, **34.4 MB**. Content: `index.html`, glue `pdfcraft-web-<hash>.js` (183 KB), `pdfcraft-web-<hash>_bg.wasm` (**60.6 MB**), the **OCR worker** (`pdfcraft-ocr-worker-<hash>.js`, 3.4 MB `_bg.wasm`, `_loader.js`), `models/` (two ONNX models, 12.2 MB, `CC-BY-SA-4.0`), `snippets/`, licences, `HOSTING.md`. 76.6 MB unpacked, 23 files. |
| Size | Under the Kazo OS limits (128 MiB per file; package of 64 MiB compressed, 128 MiB unpacked): the `.wasm` is the size of WordCraft's. Above GitHub's 50 MB warning, under its 100 MB refusal (known pitfall, already accepted for Kazo Word). |
| Rendering | `eframe::Renderer::Glow`: **WebGL 2 only** (`HOSTING.md` talks about wgpu, but the 0.21.1 code does not use it). `?webgl` changes nothing; the guide's WebGPU journey does not apply. |
| Canvas | `pdfcraft` (not `pdfcraft_canvas`), hard-coded: one instance per HTML document, so one iframe per window. |
| Host mode | `data-host="page"` on the canvas: `pdfcraft:*` `CustomEvent`s. `ready` (`file: false`: PdfCraft reads bytes), `open { name, bytes }` (refused over 512 MiB; no name: `document.pdf`), `save { name, saveAs, bytes }`, `dirty`, `failed`. `?author=` signs comments. |
| `save` | Sent by the document's Save and Save As. **`saveAs` is right** (true for Save As, as in WordCraft): no F12 workaround as for GridCraft. After "Apply redactions" the proposed name gets `_Redacted` and `saveAs` becomes true. No `id`, no confirmation (craft-libs #611). |
| Exports | **Do not go through `save`**: Export (Word, images, text…), Print to PDF, Reduce size, signed copy, created PDF, OCR result, form data (FDF, XFDF, CSV), measurements, area snapshot call `download()`, an `<a download>` link on a `blob:`: the browser **downloads** the file outside Kazo OS. Question Q1. |
| `dirty` | True if **any** open document has changes, or an OCR review is open (as in GridCraft). |
| Commands (`window.pdfcraft`) | Installed only with **`?control`** in the page address. `request`, `execute` (`ui.command { id }`, **no parameters**), `commands`, `docInspect` (`ui.state`), `uiInspect`, `screenshot`. Documents are in **`ui.state`**: `documents[{ tab, doc, name, path, pages, dirty, active }]`; `ui.inspect` is the accessibility tree. Theme: `ui.set { key: "theme", value: "dark" }`; language: `ui.set { key: "language", value }`. Commands: `file.save`, `file.save_as`, `file.close`, `file.close_all`, `file.open`…; no `file.new` and no `file.rename`. |
| Browser picker (`rfd`) | About twenty uses: open (PDF, images, text), insert an image or a page, TTF/OTF font, `.p12`/`.pfx` digital ID, signature image, form data, files to combine. All go through the same `input type=file`: the module's picker replaces them. |
| Formats | Opened: `.pdf`; images (`png jpg jpeg tif tiff gif bmp jp2 j2k jpx pcx`) and text (`txt text`) open as **new unsaved PDFs**. Saved: `.pdf` only. No Office documents on the web. |
| OCR | "Recognize text" loads `models/*.onnx` **relative to the page** and starts module workers named by `<meta name="pdfcraft-ocr-worker">` in the page. Our iframe page must carry that tag and find `models/` next to it. About 600 MB of memory per worker (two at most). Without the models, everything else works. Question Q2. |
| Settings | `localStorage` `pdfcraft` (eframe); the module keeps them per Kazo user. |
| Closing | PdfCraft warns on `beforeunload` when work is unsaved; the window's close guard comes first. |
| Language | 26 languages (`en ja zh-hans zh-hant cs pt-br de es fr ru kk bg te hu uk it ar ckb fa pl sl el tr lt id vi`); Chinese has no font in the web build (upstream gap 5). |
| Window size | Desktop: 1440 × 920, minimum 480 × 520. |
| Brand | ArtCraft name and logo: trademarks. A **modified** build removes them; the Store listing says "based on PdfCraft by the ArtCraft team", with no logo. |

## Decisions

| # | Topic | Decision |
| --- | --- | --- |
| 1 | Name and identity | "**Kazo Pdf**" (Benoit, 2026-11-13). Id `ca.kazo.pdf`, publisher `pub.kazo`, assembly `Kazo.Pdf`, category `productivity`, several windows, 1200 × 820 window (the desktop's 1440 × 920 is larger than a laptop screen once the taskbar counts), minimum content 480 × 520. |
| 2 | Shared code | Module `Kazo.Os.AppSdk.Craft` (ADR 0259). Minimum version: **Kazo OS 3.3.3** (module, `canChangeScreen` from ADR 0263, `?autolaunch`), unless Q1 needs a newer one. |
| 3 | Screen change | `canChangeScreen: false` in the manifest from work item 1 (issue [#2](https://github.com/Kazo-ca/Os-App-PDF/issues/2)). |
| 4 | Integration | One iframe per window, page `wwwroot/host/index.html` with the canvas `pdfcraft` `data-host="page"` `data-craft-app="pdfcraft"`, parameter `control` for `window.pdfcraft`. `models/` and the OCR worker must resolve relative to the page: the page carries a `<base>` to `../pdfcraft/` (or the build sits next to it), and `tools/vendor-pdfcraft.sh` writes the `pdfcraft-ocr-worker` tag (the worker name has a hash). Chosen in work item 1. |
| 5 | Build provenance | As in Kazo Spreadsheets: first the **official 0.21.1 zip** pinned by SHA-256 in `wwwroot/pdfcraft/` with `pdfcraft.json`; then built from the fork `Kazo-ca/pdfcraft` (branch `kazo/v0.21.1`). |
| 6 | Close guard | Read from `ui.state` (`documents[{ doc, name, path, dirty }]`), not from `ui.inspect`. The window reads it itself until the module's `OpenDocument.ListFrom` knows this third shape (feedback sent to the module). |
| 7 | Theme and language | `ui.set { theme }` on every OS theme change, `ui.set { language }` at startup (`CraftLanguage.Pick` on the list of 26 codes). |
| 8 | File picker | The module does not say why PdfCraft asks for a file (open, insert pages, compare, image to place…). Every picked file is handed to PdfCraft; a single picked `.pdf` is bound to the window under its name only when no document already has that name. |
| 9 | Benoit's answers (2026-11-13) | Q1 = **A** (the module intercepts exports, ADR 0264 in `Kazo-ca/Os`), Q2 = **A** (OCR models in the package), Q3 = **B** (`.pdf` only). |
| 10 | Exports | PdfCraft exports through an `<a download>` link: the Kazo OS Craft module hands them to the window (`CraftFrame.Exporting`, [ADR 0264](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0264-craft-app-exports-reach-the-window.md), series `craft-exports/base` of `Kazo-ca/Os`), which always saves them as **copies** (`DocumentSession.DecideCopy`), even a `.pdf`. So Kazo Pdf asks for **Kazo OS 3.3.4**, the version that brings ADR 0264 (work item 4). |

## Questions asked to Benoit

Answered on 2026-11-13: Q1 = A, Q2 = A, Q3 = B (decision 9).

**Q1. Exports go to the browser's downloads.** Export, Print to PDF, Reduce size, signed copy,
OCR, form data: PdfCraft "downloads" them through an `<a download>` link instead of handing them
to the host. In Kazo OS the file lands in the real computer's Downloads folder, not in the Kazo OS
file system.

- **A (recommended)**: the `Kazo.Os.AppSdk.Craft` module intercepts these downloads in the iframe
  (as it already does for the file picker) and hands them to the window as a **copy** (OS save
  dialog, the binding does not change). An ADR 0264 and a work item in `Kazo-ca/Os`; every Craft
  app benefits. Kazo Pdf then requires the OS version that brings it (after 3.3.3), so its Store
  release waits for that version. Meanwhile Claude prepares the upstream issue (send exports
  through `save` in host mode), which Benoit publishes.
- B: fix upstream only (issue and PR prepared by Claude, published by Benoit); Kazo Pdf ships on
  3.3.3 with the limit written in the listing until the PdfCraft version that fixes it.
- C: fix in our fork (work item 2), hence a modified build from v1 (ArtCraft marks removed).

**Q2. The OCR models (12.2 MB, `CC-BY-SA-4.0`).**

- **A (recommended)**: ship them in the package (34 MB compressed instead of about 22); text
  recognition works offline; licence and attribution in `THIRD-PARTY-NOTICES.md` (an allowed
  licence).
- B: leave them out; "Recognize text" says the download failed (limit written in the listing).

**Q3. Formats associated with Kazo Pdf.**

- **A (recommended)**: default app for `.pdf`; "Open with" for images
  (`.png .jpg .jpeg .tif .tiff .gif .bmp .jp2 .j2k .jpx .pcx`) and `.txt`, which open as a new PDF
  (the OS viewers and editor stay the default).
- B: `.pdf` only.

## Work items

Series `pdf/base`, one branch `pdf/<n>-<topic>` and one PR to `pdf/base` per work item. Benoit
merges `pdf/base` into `main`. The module does a large part of the guide's work items 1 to 3, and
Kazo Spreadsheets already wrote a full window on it: the solution and the window make one work
item here.

| No. | Branch | Content | Depends on |
| --- | --- | --- | --- |
| 0 | `pdf/0-plan` | this plan (fr and en) | — |
| 1 | `pdf/1-solution` | renamed solution (`Kazo.Pdf`, `.Os`, `.Tests`), manifest (`canChangeScreen: false`, OS 3.3.3, `.pdf` only), `tools/vendor-pdfcraft.sh` (pinned 0.21.1 zip, models per Q2, worker tag) and `pdfcraft.json`, iframe page; window on the module: open, save, Save As, close guard (`ui.state`), picker, shortcuts, theme, language, author, settings; two windows; xUnit tests and Playwright journeys; OS submodule on `main` (3.3.3) until the `v3.3.3` tag | plan merged |
| 2 | `pdf/2-build-from-source` | fork `Kazo-ca/pdfcraft` as a submodule (branch `kazo/v0.21.1`), `tools/build-pdfcraft.sh` (pinned `craft-i18n`, `cargo xtask models`), workflow `build-pdfcraft.yml`, commit test | work item 1 |
| 3 | `pdf/3-exports` | per Q1: the window receives exports as copies (A, after the OS work item), or nothing here (B) | work item 1, answer to Q1 |
| 4 | `pdf/4-store` | icon without the ArtCraft logo, fr and en listing, README rebuilt on the listing (`store:readme`, "Try it" with `?autolaunch=ca.kazo.pdf`), `THIRD-PARTY-NOTICES.md`, install in a published Kazo OS, tag through "Tag a release", catalogue (signing in `Os-AppMarketPlace`), `features.md` | work items 1 to 3, required OS version |

**Done when**: Kazo Pdf installs from the Store of a published Kazo OS, opens a `.pdf` by
double-click, saves it, and two windows run together.

## Upstream track

Texts prepared by Claude in English, published by Benoit (no mention of an agent):

- PdfCraft: in host mode, hand exports to the host through `save` (Q1);
- PdfCraft: `HOSTING.md` talks about wgpu and `?webgl`/`?cpu`, while the web build uses Glow;
- `craft-libs`: join #611 (save confirmation) and #610 (`open-request`).

## Progress

- [x] Step 0: reading PdfCraft 0.21.1 and the official zip.
- [x] Plan fr and en.
- [x] Benoit's answers to Q1, Q2 and Q3 (A, A, B).
- [x] Work item 1: solution and window (`pdf/1-solution`).
- [x] Work item 2: build from the fork `Kazo-ca/pdfcraft` (`pdf/2-build-from-source`).
- [x] Work item 3: exports as copies (`pdf/3-exports`, Kazo OS ADR 0264).
- [x] Work item 4: Store listing, notices, install in the published Kazo OS 3.3.4 (`pdf/4-store`).
- [ ] Release: `pdf/base` into `main` (Benoit), tag `v0.1.0`, catalogue, `features.md`.
