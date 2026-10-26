// Prepares a published Blazor WebAssembly app for GitHub Pages.
// Usage: node tools/prepare-pages.mjs <publish/wwwroot> <base path, e.g. /TSA/>
//
// - Sets <base href> in index.html to the repository sub-path.
// - Updates index.html's hash in service-worker-assets.js (the offline cache checks it).
// - Deletes the now-stale .br/.gz copies of those two files.
// - Copies index.html to 404.html so deep links load the app.
// - Adds .nojekyll so GitHub Pages serves the _framework folder.
import { readFileSync, writeFileSync, copyFileSync, rmSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join } from 'node:path';

const [root, basePath] = process.argv.slice(2);

if (!root || !basePath?.startsWith('/') || !basePath.endsWith('/')) {
    console.error('Usage: node tools/prepare-pages.mjs <wwwroot> </base/path/>');
    process.exit(1);
}

const indexPath = join(root, 'index.html');
const html = readFileSync(indexPath, 'utf8');

if (!html.includes('<base href="/" />')) {
    throw new Error('index.html does not contain <base href="/" />');
}

writeFileSync(indexPath, html.replace('<base href="/" />', `<base href="${basePath}" />`));

const hash = 'sha256-' + createHash('sha256').update(readFileSync(indexPath)).digest('base64');
const manifestPath = join(root, 'service-worker-assets.js');
const manifest = readFileSync(manifestPath, 'utf8');
const pattern = /("hash":\s*")[^"]+("\s*,\s*"url":\s*"index\.html")/;

if (!pattern.test(manifest)) {
    throw new Error('index.html entry not found in service-worker-assets.js');
}

writeFileSync(manifestPath, manifest.replace(pattern, `$1${hash}$2`));

// Pre-compressed copies of the edited files are now stale.
for (const file of ['index.html', 'service-worker-assets.js']) {
    for (const ext of ['.br', '.gz']) {
        rmSync(join(root, file + ext), { force: true });
    }
}

copyFileSync(indexPath, join(root, '404.html'));
writeFileSync(join(root, '.nojekyll'), '');

console.log(`Prepared ${root} for ${basePath}`);
