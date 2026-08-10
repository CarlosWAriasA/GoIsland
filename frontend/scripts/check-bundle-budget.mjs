import { readFile, stat } from 'node:fs/promises';
import { resolve } from 'node:path';

const limits = {
  script: 320 * 1024,
  stylesheet: 120 * 1024,
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
  const { size } = await stat(file);
  const actualKb = (size / 1024).toFixed(1);
  const limitKb = (limit / 1024).toFixed(0);
  if (size > limit) {
    throw new Error(`${label}: ${actualKb} KB supera el presupuesto de ${limitKb} KB.`);
  }
  console.log(`${label}: ${actualKb} KB / ${limitKb} KB.`);
}
