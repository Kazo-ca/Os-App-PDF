// The Kazo OS Store's screenshots (store/screenshots/), taken by install-and-open.mjs when it is
// given a screenshots folder, in the OS where Kazo Pdf was just installed. The document is the one
// sample-pdf.mjs writes (our own text and shapes), so no third-party content appears.

const STEP_MS = 60_000;

/** egui paints on input: move the pointer over the document so the last frame is drawn. */
async function repaint(page, id, windowById) {
    const box = await windowById(page, id).locator('iframe.craft-frame-iframe').boundingBox();
    for (let step = 0; step < 6; step++) {
        await page.mouse.move(box.x + box.width / 2 + step * 7, box.y + box.height - 60 - step * 5);
        await page.waitForTimeout(400);
    }
}

export async function takeScreenshots({ page, second, reopened, command, shoot, windowById, openApp, HOME }) {
    const chrome = windowById(page, second).locator('header.window-chrome').first();

    // The File Explorer of the journey is reopened at the end, listing the files.
    for (const close of await page.locator('article[data-window-id]', { hasText: 'File Explorer' }).locator('header.window-chrome .chrome-button-close').all()) {
        await close.dispatchEvent('click');
    }

    // 1. The report, the window maximized, the whole first page in view.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await command(reopened, 'view.fit_one_page');
    await repaint(page, second, windowById);
    await shoot(page, '1.png');

    // 2. In a window of the desktop, an export to Word: the OS "Save a copy" dialog.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await command(reopened, 'view.fit_width_scrolling');
    await repaint(page, second, windowById);
    await command(reopened, 'export.docx');
    const dialog = page.locator('section.file-dialog', { hasText: 'Save a copy' });
    await dialog.waitFor({ state: 'visible', timeout: STEP_MS });
    await dialog.getByLabel('File name').fill('Garden club report.docx');
    await shoot(page, '2.png');
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_MS });

    // 3. The File Explorer in front: PDFs open in Kazo Pdf with a double-click.
    await openApp(page, 'File Explorer');
    const row = page.locator(`tr[data-item-path="${HOME}/report.pdf"]`).first();
    await row.waitFor({ timeout: STEP_MS });
    await row.click({ position: { x: 40, y: 10 } });
    await shoot(page, '3.png');
}
