// A small PDF written from scratch for the installed journey and the Store screenshots: our own
// text and shapes, drawn with the standard Helvetica font (not embedded; PdfCraft draws it with its
// own open substitute), so no third-party content appears in the screenshots.

const PAGE = '/Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >>';

/** PDF text string: escapes the characters the literal string syntax reserves. */
const text = value => `(${value.replace(/[\\()]/g, character => `\\${character}`)})`;

/** One line of text at (x, y). */
const line = (font, size, x, y, value) => `BT /${font} ${size} Tf ${x} ${y} Td ${text(value)} Tj ET`;

function firstPage() {
    const bars = [['North', 0.16, 0.45, 0.82, 310], ['South', 0.93, 0.42, 0.33, 220], ['East', 0.24, 0.66, 0.42, 265], ['West', 0.62, 0.38, 0.78, 180]];
    return [
        '0.11 0.07 0.07 rg 0 762 595 80 re f',
        '1 1 1 rg', line('F2', 26, 50, 792, 'Garden Club Quarterly Report'),
        '0.85 0.78 0.76 rg', line('F1', 12, 50, 774, 'Third quarter - prepared with Kazo Pdf'),
        '0.15 0.15 0.15 rg', line('F2', 15, 50, 715, 'Summary'),
        line('F1', 11, 50, 694, 'Membership grew in every district this quarter. The seed library lent'),
        line('F1', 11, 50, 679, 'more packets than ever, and the spring plant sale paid for two new'),
        line('F1', 11, 50, 664, 'raised beds at the community garden.'),
        line('F2', 15, 50, 620, 'Seed packets lent, by district'),
        '0.75 0.75 0.75 RG 1 w 80 360 m 80 600 l 545 600 m 545 600 l S 80 360 m 545 360 l S',
        ...bars.flatMap(([name, r, g, b, height], index) => {
            const x = 110 + index * 110;
            return [`${r} ${g} ${b} rg ${x} 360 70 ${height * 0.7} re f`, '0.15 0.15 0.15 rg',
                line('F1', 11, x + 14, 342, name), line('F2', 11, x + 20, 366 + height * 0.7, String(height))];
        }),
        '0.15 0.15 0.15 rg', line('F2', 15, 50, 290, 'Next quarter'),
        line('F1', 11, 50, 269, '- Open the tool shed on Saturday mornings.'),
        line('F1', 11, 50, 254, '- Plant the pollinator strip along the north fence.'),
        line('F1', 11, 50, 239, '- Start a composting workshop for new members.'),
        '0.6 0.6 0.6 rg', line('F1', 9, 50, 40, 'Garden Club - page 1 of 2'),
    ].join('\n');
}

function secondPage() {
    const rows = [['Seed library', '412', '+18%'], ['Plant sale', '1 280', '+9%'], ['Workshops', '6', '+2'], ['New members', '37', '+12']];
    return [
        '0.15 0.15 0.15 rg', line('F2', 18, 50, 780, 'Figures'),
        '0.95 0.93 0.92 rg 50 700 495 30 re f',
        '0.15 0.15 0.15 rg', line('F2', 11, 62, 711, 'Item'), line('F2', 11, 330, 711, 'This quarter'), line('F2', 11, 460, 711, 'Change'),
        ...rows.flatMap(([item, value, change], index) => {
            const y = 676 - index * 30;
            return ['0.85 0.85 0.85 RG 0.5 w', `50 ${y - 9} m 545 ${y - 9} l S`, '0.15 0.15 0.15 rg',
                line('F1', 11, 62, y, item), line('F1', 11, 330, y, value), line('F1', 11, 460, y, change)];
        }),
        line('F2', 15, 50, 520, 'Notes'),
        line('F1', 11, 50, 499, 'Volunteers logged 140 hours in the garden. Thank you to everyone who'),
        line('F1', 11, 50, 484, 'watered, weeded and welcomed visitors.'),
        '0.6 0.6 0.6 rg', line('F1', 9, 50, 40, 'Garden Club - page 2 of 2'),
    ].join('\n');
}

/** The PDF's bytes: two pages, with a correct cross-reference table. */
export function samplePdf() {
    const pages = [firstPage(), secondPage()];
    const objects = [
        '<< /Type /Catalog /Pages 2 0 R >>',
        '<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>',
        `<< ${PAGE} /Contents 7 0 R >>`,
        `<< ${PAGE} /Contents 8 0 R >>`,
        '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>',
        '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>',
        ...pages.map(content => `<< /Length ${Buffer.byteLength(content, 'latin1')} >>\nstream\n${content}\nendstream`),
    ];
    let body = '%PDF-1.7\n';
    const offsets = [];
    objects.forEach((object, index) => {
        offsets.push(Buffer.byteLength(body, 'latin1'));
        body += `${index + 1} 0 obj\n${object}\nendobj\n`;
    });
    const xref = Buffer.byteLength(body, 'latin1');
    body += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
    body += offsets.map(offset => `${String(offset).padStart(10, '0')} 00000 n \n`).join('');
    body += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
    return Buffer.from(body, 'latin1');
}
