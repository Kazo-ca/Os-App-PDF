// End-to-end runs of opening, saving, Save As and closing (doc/en/plan.md, work item 1, decisions
// 6 and 8), inside the Kazo OS host: `npm run test:e2e` after `dotnet build Kazo.Pdf.slnx -c Debug`.
//
// PdfCraft is driven through its page API (window.pdfcraft in the iframe) to create a document and
// run its commands, as its menus and shortcuts do; everything else goes through the OS: its save and
// open dialogs, its message box, the window's title bar and close button, and the Terminal
// (`xdg-open` starts the app associated with the file type).
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { openApp, runInTerminal, startHost } from './host.mjs';
import {
    HOME, STEP_TIMEOUT_MS, answerSaveDialog, bootPage, command, expectTitle, frameOf, launchBrowser, newModifiedDocument, pdfWindows, shot,
    waitForDocument, waitForWindow, windowById,
} from './common.mjs';

const SAVE_TITLE = 'Save the document';

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

/** Waits for the Terminal's output to show `text`. */
async function expectInTerminal(page, text) {
    await page.locator('.xterm-rows').last().getByText(text, { exact: false }).first().waitFor({ timeout: STEP_TIMEOUT_MS });
}

/** Adds a blank page to the active document. */
async function addPage(frame, pages) {
    await command(frame, 'page.insert_blank');
    await waitForDocument(frame, doc => doc.active && doc.pages === pages, `with ${pages} pages`);
}

test('a new PDF is saved where the user says, saved again in place, and reopens from the OS', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await newModifiedDocument(frame);
    await expectTitle(page, id, '*Untitled — Kazo Pdf');

    // The first save of a new document asks where to write it.
    await command(frame, 'file.save');
    await answerSaveDialog(page, SAVE_TITLE, 'report.pdf');
    await expectTitle(page, id, 'report.pdf — Kazo Pdf');
    await waitForDocument(frame, doc => doc.active && !doc.dirty, 'clean after the save');

    // The next save writes the same file, with no dialog.
    await addPage(frame, 3);
    await expectTitle(page, id, '*report.pdf — Kazo Pdf');
    await command(frame, 'file.save');
    await expectTitle(page, id, 'report.pdf — Kazo Pdf');
    assert.equal(await page.locator('section.file-dialog').count(), 0);

    // `xdg-open` starts the app associated with .pdf: a second window, on the file.
    await runInTerminal(page, `xdg-open ${HOME}/report.pdf`);
    const second = await waitForWindow(page, 2, [id]);
    await expectTitle(page, second, 'report.pdf — Kazo Pdf');
    await waitForDocument(await frameOf(page, second), doc => doc.name === 'report.pdf' && doc.pages === 3, 'report.pdf with three pages');

    assert.deepEqual(errors, []);
    await page.close();
});

test('Save As asks for a new file and binds the document to it', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await newModifiedDocument(frame);
    await command(frame, 'file.save');
    await answerSaveDialog(page, SAVE_TITLE, 'first.pdf');
    await expectTitle(page, id, 'first.pdf — Kazo Pdf');

    await command(frame, 'file.save_as');
    await answerSaveDialog(page, SAVE_TITLE, 'second.pdf');
    await expectTitle(page, id, 'second.pdf — Kazo Pdf');

    // A plain save now writes second.pdf in place.
    await addPage(frame, 3);
    await expectTitle(page, id, '*second.pdf — Kazo Pdf');
    await command(frame, 'file.save');
    await expectTitle(page, id, 'second.pdf — Kazo Pdf');

    await runInTerminal(page, `ls ${HOME}`);
    await expectInTerminal(page, 'second.pdf');
    assert.match(await page.locator('.xterm-rows').last().innerText(), /first\.pdf/);

    assert.deepEqual(errors, []);
    await page.close();
});

test('closing with changes asks; Cancel keeps the window, Yes saves before closing', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await newModifiedDocument(frame);
    await expectTitle(page, id, '*Untitled — Kazo Pdf');

    const close = windowById(page, id).locator('header.window-chrome .chrome-button-close');
    const question = page.locator('.message-box-dialog', { hasText: 'Save the changes' });

    // Cancel: nothing closes, nothing is lost.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Cancel' }).click();
    await question.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);
    await waitForDocument(frame, doc => doc.dirty && doc.pages === 2, 'still modified');

    // Yes, then Cancel in the save dialog: the window stays open, and the message says so.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    const saveDialog = page.locator('section.file-dialog', { hasText: SAVE_TITLE });
    await saveDialog.waitFor({ timeout: STEP_TIMEOUT_MS });
    await saveDialog.getByRole('button', { name: 'Cancel' }).click();
    await windowById(page, id).locator('.pdf-notice', { hasText: 'was not saved' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);

    // Yes, then a file name: the document is written, then the window closes.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    await answerSaveDialog(page, SAVE_TITLE, 'closing.pdf');
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await runInTerminal(page, `xdg-open ${HOME}/closing.pdf`);
    const reopened = await waitForWindow(page, 1);
    await expectTitle(page, reopened, 'closing.pdf — Kazo Pdf');
    await waitForDocument(await frameOf(page, reopened), doc => doc.pages === 2, 'closing.pdf with two pages');

    assert.deepEqual(errors, []);
    await page.close();
});

test('PdfCraft\'s Open shows the OS dialog, and the picked PDF is bound to the window', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Pdf');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await newModifiedDocument(frame);
    await command(frame, 'file.save');
    await answerSaveDialog(page, SAVE_TITLE, 'picked.pdf');
    await windowById(page, id).locator('header.window-chrome .chrome-button-close').click();
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await openApp(page, 'Kazo Pdf');
    const fresh = await waitForWindow(page, 1);
    const empty = await frameOf(page, fresh);
    await command(empty, 'file.open');
    const dialog = page.locator('section.file-dialog[role="dialog"]').last();
    await dialog.getByRole('heading', { name: 'Choose a file' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await dialog.getByText('picked.pdf', { exact: true }).dblclick().catch(async error => {
        await shot(page, 'open-dialog');
        throw error;
    });
    await dialog.waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await waitForDocument(empty, doc => doc.name === 'picked.pdf' && doc.pages === 2, 'picked.pdf');
    await expectTitle(page, fresh, 'picked.pdf — Kazo Pdf');
    assert.equal(await pdfWindows(page).count(), 1);

    assert.deepEqual(errors, []);
    await page.close();
});
