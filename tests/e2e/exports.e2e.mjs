// End-to-end runs of PdfCraft's exports (doc/en/plan.md, work item 3; ADR 0264 of Kazo OS) inside
// the Kazo OS host: `npm run test:e2e` after `dotnet build Kazo.Pdf.slnx -c Debug`. PdfCraft exports
// with a browser download link; the module hands the file to the window, which saves it as a copy
// through the OS dialog. The browser must download nothing.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { openApp, runInTerminal, startHost } from './host.mjs';
import {
    HOME, STEP_TIMEOUT_MS, answerSaveDialog, bootPage, command, expectTitle, frameOf, launchBrowser, newModifiedDocument, waitForWindow, windowById,
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

test('an export goes through the OS dialog as a copy, and the window keeps its document', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const downloads = [];
    page.on('download', download => downloads.push(download.suggestedFilename()));

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await newModifiedDocument(frame);
    await command(frame, 'file.save');
    await answerSaveDialog(page, 'Save the document', 'report.pdf');
    await expectTitle(page, id, 'report.pdf — Kazo Pdf');

    // Export › Word: PdfCraft's download() of report.docx.
    await command(frame, 'export.docx');
    await answerSaveDialog(page, 'Save a copy', 'report.docx');
    await windowById(page, id).locator('.pdf-notice', { hasText: 'Copy saved: report.docx.' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await expectTitle(page, id, 'report.pdf — Kazo Pdf');

    // Cancelling an export's dialog leaves everything as it was.
    await command(frame, 'export.html');
    const dialog = page.locator('section.file-dialog', { hasText: 'Save a copy' });
    await dialog.waitFor({ timeout: STEP_TIMEOUT_MS });
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
    await expectTitle(page, id, 'report.pdf — Kazo Pdf');

    await runInTerminal(page, `ls ${HOME}`);
    await page.locator('.xterm-rows').last().getByText('report.docx', { exact: false }).first().waitFor({ timeout: STEP_TIMEOUT_MS });
    assert.doesNotMatch(await page.locator('.xterm-rows').last().innerText(), /report\.html/);
    assert.deepEqual(downloads, [], 'the browser downloaded nothing');

    assert.deepEqual(errors, []);
    await page.close();
});
