import { readFile } from 'node:fs/promises';
import { gzipSync } from 'node:zlib';
import { resolve } from 'node:path';

// El presupuesto se mide sobre el tamaño comprimido porque es lo que el navegador
// descarga. Medirlo en crudo penalizaba el uso de custom properties: `var(--space-3)`
// ocupa más texto que `0.75rem`, pero al repetirse cientos de veces gzip lo reduce
// casi a cero, así que el CSS crudo crecía mientras el transferido bajaba.
const limits = {
  script: 100 * 1024,
  stylesheet: 24 * 1024,
};

const root = resolve(import.meta.dirname, '..');
const html = await readFile(resolve(root, 'dist', 'index.html'), 'utf8');
const scriptPath = html.match(/<script[^>]+src="([^"]+)"/)?.[1];
const stylesheetPath = html.match(/<link[^>]+rel="stylesheet"[^>]+href="([^"]+)"/)?.[1]
  ?? html.match(/<link[^>]+href="([^"]+)"[^>]+rel="stylesheet"/)?.[1];

if (!scriptPath || !stylesheetPath) {
  throw new Error('No se encontraron los activos principales en dist/index.html.');
}

const assets = [
  ['JavaScript principal', scriptPath, limits.script],
  ['CSS principal', stylesheetPath, limits.stylesheet],
];

for (const [label, publicPath, limit] of assets) {
  const file = resolve(root, 'dist', publicPath.replace(/^\//, ''));
  const size = gzipSync(await readFile(file), { level: 9 }).byteLength;
  const actualKb = (size / 1024).toFixed(1);
  const limitKb = (limit / 1024).toFixed(0);
  if (size > limit) {
    throw new Error(`${label}: ${actualKb} KB gzip supera el presupuesto de ${limitKb} KB.`);
  }
  console.log(`${label}: ${actualKb} KB / ${limitKb} KB gzip.`);
}
