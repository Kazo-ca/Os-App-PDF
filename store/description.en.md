# Kazo Pdf

A PDF reader and editor for Kazo OS. Kazo Pdf is based on
[PdfCraft](https://github.com/storytold/pdfcraft) by the ArtCraft team, an open-source PDF editor
written in Rust, which runs here in a Kazo OS window.

## Read and edit

- Read, search, zoom, and browse pages, bookmarks and thumbnails.
- Annotate: highlight, comment, draw, stamps, measurements.
- Edit text and images; reorder, rotate, insert, extract and delete pages; combine several files.
- Fill in and create forms, sign, protect with a password, redact.
- Recognize the text of a scanned document (OCR): the models ship with the app, nothing goes to the
  Internet.
- Several windows at once, each with its own documents.

## Your Kazo OS files

- Double-click a `.pdf` file in the File Explorer: it opens in Kazo Pdf.
- Save rewrites the open PDF. Save As (Ctrl+Shift+S) and the first save of a new document go
  through the Kazo OS save dialog.
- Exports (Word, images, text, Print to PDF, Reduce size, signed copy, form data, OCR result…) go
  through the Kazo OS "Save a copy" dialog: the document stays bound to its file.
- Closing the window asks before losing changes, for each open document.
- File › Open, and the images, fonts and digital IDs to insert, come from the Kazo OS files,
  through the Kazo OS dialog.
- Kazo Pdf follows the OS theme (light or dark) and language; its settings are kept per Kazo OS
  user in `~/.config/ca.kazo.pdf/`.

## Known limits

- **PdfCraft is in alpha.** Most documents open and save fine, but keep a copy of the important
  ones.
- **Asian scripts**: the web version of PdfCraft has no Chinese, Japanese or Korean font; that text
  may show as boxes in the interface, and writing Japanese into a PDF returns an error.
- PdfCraft draws with **WebGL 2**: a browser or computer without WebGL 2 shows an error screen
  instead of the document.
- The download is about 34 MB (77 MB once installed), and each window loads its own copy of
  PdfCraft. Text recognition needs a lot of memory (about 600 MB per task).
- The window stays on the screen it opened on: moving it to another screen would reload PdfCraft
  and lose the open documents.
- Requires Kazo OS 3.3.4 or later.

## License

Kazo Pdf is free software under the MIT license. PdfCraft is under the MIT or Apache-2.0 license;
its notices, the licenses of the fonts it contains and the license of the text recognition models
(CC-BY-SA-4.0, Robert Knight) ship with the app (see `THIRD-PARTY-NOTICES.md` in the repository).
The ArtCraft name and logos are trademarks of the ArtCraft team and are not used by Kazo Pdf.
