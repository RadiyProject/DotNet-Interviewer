# PDF.js 6.4.299

Mozilla PDF.js, legacy browser build; Apache-2.0 (see LICENSE and the retained file notices).

Upstream: https://github.com/mozilla/pdf.js/releases/tag/v6.4.299
Distribution: https://www.npmjs.com/package/pdfjs-dist/v/6.4.299
Files copied without modifications from https://cdn.jsdelivr.net/npm/pdfjs-dist@6.4.299/legacy/build/ (pdf.min.mjs and pdf.worker.min.mjs).

Used only for extracting resume text. No viewer, rendering, scripting manager, CDN requests at runtime or external AI is involved. PDF.js and its worker are served by the local trainer and loaded only when a PDF is selected. Scanned-image OCR is not included. Review extracted text before saving, especially multi-column documents.

The application reads `page.streamTextContent()` using `getReader().read()` instead of `getTextContent()`. This avoids the latter's dependency on `ReadableStream[Symbol.asyncIterator]` in Safari; no global prototype polyfill or vendor modifications are needed. Upstream issue: https://github.com/mozilla/pdf.js/issues/20973

SHA-256:
bccc24ea711db8e44503629519904a5292d73b9daaa214bbe7cdcc282b0f4259  pdf.min.mjs
145d2dd3ab0c86151011dba95acfa2d5336e2accd59388ea43dbee0efddaaec6  pdf.worker.min.mjs
