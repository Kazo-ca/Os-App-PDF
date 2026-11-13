// Shared helpers of the end-to-end runs: the browser, the Kazo Pdf windows and PdfCraft's
// page API (window.pdfcraft in each iframe, craft-web-api).
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { boot } from './host.mjs';

// PdfCraft's first start downloads 61 MB of .wasm from the local host and compiles it.
export const READY_TIMEOUT_MS = 120_000;
export const STEP_TIMEOUT_MS = 20_000;
export const HOME = '/home/neo';

// PdfCraft draws with WebGL 2 (Glow): SwiftShader gives headless Chromium one.
export const launchBrowser = () => chromium.launch({
    args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'],
});

/** A fresh page with the OS booted; `errors` collects the page's uncaught errors. */
export async function bootPage(browser, baseUrl) {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await boot(page, baseUrl);
    return { page, errors };
}

/** Saves a screenshot of the page when PDF_E2E_SHOTS names a folder. */
export async function shot(page, name) {
    if (process.env.PDF_E2E_SHOTS) {
        await page.screenshot({ path: `${process.env.PDF_E2E_SHOTS}/${name}.png` });
    }
}

/** Kazo Pdf's OS windows, in opening order. */
export const pdfWindows = page => page.locator('article[data-window-id]', { has: page.locator('.pdf-window') });

/** One OS window by its id. */
export const windowById = (page, id) => page.locator(`article[data-window-id="${id}"]`);

/** Waits for `count` Kazo Pdf windows to show PdfCraft; returns the id of the newest one. */
export async function waitForWindow(page, count, known = []) {
    try {
        await page.locator('.pdf-frame.running').nth(count - 1).waitFor({ timeout: READY_TIMEOUT_MS });
    } catch (error) {
        await shot(page, `window-${count}`);
        throw error;
    }
    const ids = await pdfWindows(page).evaluateAll(nodes => nodes.map(node => node.getAttribute('data-window-id')));
    assert.equal(ids.length, count);
    return ids.find(id => !known.includes(id));
}

/** The iframe element of a window. */
export const iframeOf = (page, id) => windowById(page, id).locator('iframe.craft-frame-iframe');

/** The PdfCraft frame of a window. */
export async function frameOf(page, id) {
    const handle = await iframeOf(page, id).elementHandle();
    return handle.contentFrame();
}

/** Runs a method of PdfCraft's control API in a frame. */
export const request = (frame, method, params = {}) =>
    frame.evaluate(([name, args]) => window.pdfcraft.request(name, args), [method, params]);

/** Runs a PdfCraft command (as its menus do) in a frame. */
export const command = (frame, id) => request(frame, 'ui.command', { id });

/** PdfCraft's open documents (ui.state). */
export const documentsOf = async frame => (await request(frame, 'ui.state')).documents ?? [];

/** Waits until one of the frame's documents matches `predicate` (a function of a ui.state document). */
export async function waitForDocument(frame, predicate, description) {
    const deadline = Date.now() + STEP_TIMEOUT_MS;
    let last = [];
    while (Date.now() < deadline) {
        last = await documentsOf(frame).catch(() => []);
        if (last.some(predicate)) {
            return;
        }
        await new Promise(resolve => setTimeout(resolve, 200));
    }
    throw new Error(`no document ${description}: ${JSON.stringify(last)}`);
}

/** Creates a blank one-page PDF and adds a page, so that it has unsaved changes. */
export async function newModifiedDocument(frame) {
    await command(frame, 'create.blank');
    await waitForDocument(frame, doc => doc.active, 'active after create.blank');
    await command(frame, 'page.insert_blank');
    await waitForDocument(frame, doc => doc.active && doc.pages === 2, 'with two pages');
}

/** Answers the OS save dialog with a file name (in the folder it opens in). */
export async function answerSaveDialog(page, title, fileName) {
    const dialog = page.locator('section.file-dialog', { hasText: title });
    await dialog.waitFor({ state: 'visible', timeout: STEP_TIMEOUT_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
}

/** Waits for a window's title bar to read `text`; on failure, says what it reads and the window's message. */
export async function expectTitle(page, id, text) {
    try {
        await page.waitForFunction(
            ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
            [id, text],
            { timeout: STEP_TIMEOUT_MS });
    } catch (error) {
        const actual = await windowById(page, id).locator('header.window-chrome h2').first().textContent().catch(() => null);
        const notice = await windowById(page, id).locator('.pdf-notice').textContent().catch(() => null);
        await shot(page, `title-${id}`);
        throw new Error(`title "${actual}" instead of "${text}"; notice: ${notice}`, { cause: error });
    }
}
