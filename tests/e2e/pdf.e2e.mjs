// End-to-end runs of the Kazo Pdf window inside the Kazo OS host (doc/en/plan.md, work item 1):
// `npm run test:e2e` after `dotnet build Kazo.Pdf.slnx -c Debug`. Each test opens a fresh page.
// PdfCraft draws with WebGL 2 only (its web build uses Glow).
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { openApp, startHost } from './host.mjs';
import {
    bootPage, command, documentsOf, frameOf, iframeOf, launchBrowser, pdfWindows, request, waitForDocument, waitForWindow, windowById,
} from './common.mjs';

let host;
let browser;

before(async () => {
    host = await startHost();
    browser = await launchBrowser();
});

after(async () => {
    await browser?.close();
    host?.stop();
});

test('Kazo Pdf opens from the Applications menu, with its control API, the author and the OCR worker', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    assert.equal(await page.locator('.pdf-screen').count(), 0, 'no loading or error screen is left');
    const frame = await frameOf(page, id);
    const url = new URL(frame.url());
    assert.equal(url.searchParams.has('control'), true, 'window.pdfcraft is on');
    assert.equal(url.searchParams.get('author'), 'Neo', 'the user signs notes and comments');

    // The page resolves PdfCraft's relative files (models/, OCR worker) in the build folder.
    const base = await frame.evaluate(() => document.baseURI);
    assert.match(base, /\/pdfcraft\/$/);
    const worker = await frame.evaluate(() => document.querySelector('meta[name="pdfcraft-ocr-worker"]').content);
    const response = await frame.evaluate(async file => (await fetch(file)).status, worker);
    assert.equal(response, 200);

    // A blank PDF: PdfCraft's commands reach it through the window.
    await command(frame, 'create.blank');
    await waitForDocument(frame, doc => doc.active && doc.pages === 1, 'blank and active');

    // The keyboard reaches PdfCraft: Ctrl+W closes the clean tab.
    const box = await iframeOf(page, id).boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
    await page.keyboard.press('Control+w');
    await frame.waitForFunction(async () => ((await window.pdfcraft.request('ui.state')).documents ?? []).length === 0, null, { timeout: 20_000 });

    assert.deepEqual(errors, []);
    await page.close();
});

test('the window gives PdfCraft the OS theme and language', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    const state = await request(frame, 'ui.state');
    // A fresh profile is dark and English.
    assert.equal(state.theme_preference, 'Dark');
    assert.equal(state.language, 'en');

    assert.deepEqual(errors, []);
    await page.close();
});

test('two windows run their own PdfCraft, and Ctrl+N opens another', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const firstId = await waitForWindow(page, 1);
    await openApp(page, 'Kazo Pdf');
    const secondId = await waitForWindow(page, 2, [firstId]);
    const first = await frameOf(page, firstId);
    const second = await frameOf(page, secondId);
    assert.notEqual(first, second);

    // Each PdfCraft keeps its own documents.
    await command(second, 'create.blank');
    await waitForDocument(second, doc => doc.active, 'in the second window');
    assert.equal((await documentsOf(first)).length, 0);

    // Ctrl+N in PdfCraft: the window opens a third Kazo Pdf window.
    const box = await iframeOf(page, secondId).boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
    await page.keyboard.press('Control+n');
    await waitForWindow(page, 3, [firstId, secondId]);
    assert.equal(await pdfWindows(page).count(), 3);
    assert.equal(await windowById(page, firstId).count(), 1);

    assert.deepEqual(errors, []);
    await page.close();
});
