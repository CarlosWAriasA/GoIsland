import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDirectory = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const distDirectory = resolve(projectDirectory, 'dist');
const template = await readFile(resolve(distDirectory, 'index.html'), 'utf8');
const fileEnvironment = {};
for (const fileName of ['.env', '.env.production', '.env.local', '.env.production.local']) {
  try {
    const contents = await readFile(resolve(projectDirectory, fileName), 'utf8');
    for (const line of contents.split(/\r?\n/)) {
      const match = line.match(/^\s*(VITE_(?:API|SITE)_URL)\s*=\s*(.*?)\s*$/);
      if (!match) continue;
      fileEnvironment[match[1]] = match[2].replace(/^("|')|("|')$/g, '');
    }
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
}
const apiUrl = (process.env.VITE_API_URL || fileEnvironment.VITE_API_URL)?.replace(/\/$/, '');
const deployedHostname = process.env.VERCEL_PROJECT_PRODUCTION_URL || process.env.VERCEL_URL;
const siteUrl = (
  process.env.VITE_SITE_URL
  || fileEnvironment.VITE_SITE_URL
  || (deployedHostname ? `https://${deployedHostname}` : 'http://localhost:5173')
).replace(/\/$/, '');

const publicPages = [
  { path: '/', title: 'GoIsland - Experiencias y actividades en República Dominicana', description: 'Descubre experiencias locales, actividades y recorridos para disfrutar República Dominicana.' },
  { path: '/experiences', title: 'Experiencias | GoIsland', description: 'Explora el catálogo de experiencias aprobadas disponibles en GoIsland.' },
  { path: '/contacto', title: 'Estamos para orientarte | GoIsland', description: 'Información para comunicarte con el equipo responsable de GoIsland.' },
  { path: '/privacidad', title: 'Cómo cuidamos tus datos | GoIsland', description: 'Resumen del uso de datos personales en el prototipo universitario GoIsland.' },
  { path: '/terminos', title: 'Uso de GoIsland | GoIsland', description: 'Condiciones básicas para participar en la demostración universitaria de GoIsland.' },
  { path: '/cancelaciones', title: 'Cancelaciones y reembolsos | GoIsland', description: 'Cómo funcionan las cancelaciones dentro de la demostración de GoIsland.' },
];

const escapeHtml = (value) => String(value)
  .replaceAll('&', '&amp;')
  .replaceAll('"', '&quot;')
  .replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;');

const escapeXml = (value) => escapeHtml(value).replaceAll("'", '&apos;');
const safeJson = (value) => JSON.stringify(value).replaceAll('<', '\\u003c');

const replaceMeta = (html, attribute, key, tag) => {
  const expression = new RegExp(`<meta ${attribute}="${key}"[^>]*>`, 'i');
  return expression.test(html)
    ? html.replace(expression, tag)
    : html.replace('</head>', `    ${tag}\n  </head>`);
};

const renderMetadata = ({ title, description, path, image, imageAlt, structuredData }) => {
  const canonical = `${siteUrl}${path}`;
  let html = template.replace(/<title>[\s\S]*?<\/title>/i, `<title>${escapeHtml(title)}</title>`);
  html = replaceMeta(html, 'name', 'description', `<meta name="description" content="${escapeHtml(description)}" />`);
  html = replaceMeta(html, 'property', 'og:site_name', '<meta property="og:site_name" content="GoIsland" />');
  html = replaceMeta(html, 'property', 'og:type', '<meta property="og:type" content="website" />');
  html = replaceMeta(html, 'property', 'og:title', `<meta property="og:title" content="${escapeHtml(title)}" />`);
  html = replaceMeta(html, 'property', 'og:description', `<meta property="og:description" content="${escapeHtml(description)}" />`);
  html = replaceMeta(html, 'name', 'twitter:card', `<meta name="twitter:card" content="${image ? 'summary_large_image' : 'summary'}" />`);

  const tags = [
    `<link rel="canonical" href="${escapeHtml(canonical)}" />`,
    `<meta property="og:url" content="${escapeHtml(canonical)}" />`,
    `<meta name="twitter:title" content="${escapeHtml(title)}" />`,
    `<meta name="twitter:description" content="${escapeHtml(description)}" />`,
  ];

  if (image) {
    tags.push(`<meta property="og:image" content="${escapeHtml(image)}" />`);
    tags.push(`<meta property="og:image:alt" content="${escapeHtml(imageAlt || title)}" />`);
    tags.push(`<meta name="twitter:image" content="${escapeHtml(image)}" />`);
  }
  if (structuredData) {
    tags.push(`<script type="application/ld+json">${safeJson(structuredData)}</script>`);
  }

  return html.replace('</head>', `    ${tags.join('\n    ')}\n  </head>`);
};

const getApprovedExperiences = async () => {
  if (!apiUrl) {
    console.warn('VITE_API_URL no está definido; el sitemap se generará sin experiencias.');
    return [];
  }

  const experiences = [];
  let page = 1;
  let totalPages = 1;
  do {
    const response = await fetch(`${apiUrl}/experiences?page=${page}&pageSize=100&sort=newest`);
    if (!response.ok) throw new Error(`El catálogo respondió ${response.status}.`);
    const result = await response.json();
    experiences.push(...result.items.filter((experience) => experience.isApproved === true));
    totalPages = result.totalPages;
    page += 1;
  } while (page <= totalPages);

  return experiences;
};

let experiences = [];
try {
  experiences = await getApprovedExperiences();
} catch (error) {
  if (process.env.VERCEL || process.env.PUBLIC_ASSETS_REQUIRED === 'true') throw error;
  console.warn(`No se pudo consultar el catálogo; el sitemap local no incluirá experiencias. ${error.message}`);
}

await writeFile(resolve(distDirectory, 'index.html'), renderMetadata(publicPages[0]));
for (const page of publicPages.filter((item) => item.path !== '/')) {
  const filePath = resolve(distDirectory, `${page.path.slice(1)}.html`);
  await mkdir(dirname(filePath), { recursive: true });
  await writeFile(filePath, renderMetadata(page));
}

const apiOrigin = apiUrl ? new URL(apiUrl).origin : siteUrl;
for (const experience of experiences) {
  if (!experience.slug) continue;
  const path = `/experiences/${experience.slug}`;
  const cover = experience.images.find((image) => image.isCover) || experience.images[0];
  const image = cover?.url ? new URL(cover.url, apiOrigin).toString() : undefined;
  const description = (experience.shortDescription || experience.description).slice(0, 160);
  const canonical = `${siteUrl}${path}`;
  const structuredData = {
    '@context': 'https://schema.org',
    '@type': 'TouristTrip',
    '@id': canonical,
    name: experience.title,
    description,
    url: canonical,
    ...(image ? { image } : {}),
    touristType: experience.category,
    itinerary: experience.location,
  };
  const filePath = resolve(distDirectory, `experiences/${experience.slug}.html`);
  await mkdir(dirname(filePath), { recursive: true });
  await writeFile(filePath, renderMetadata({
    title: `${experience.title} | GoIsland`,
    description,
    path,
    image,
    imageAlt: cover?.altText || `Portada de ${experience.title}`,
    structuredData,
  }));
}

const sitemapPaths = [
  ...publicPages.map((page) => page.path),
  ...experiences.filter((experience) => experience.slug).map((experience) => `/experiences/${experience.slug}`),
];
const sitemap = [
  '<?xml version="1.0" encoding="UTF-8"?>',
  '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">',
  ...sitemapPaths.map((path) => `  <url><loc>${escapeXml(`${siteUrl}${path}`)}</loc></url>`),
  '</urlset>',
  '',
].join('\n');

await writeFile(resolve(distDirectory, 'sitemap.xml'), sitemap);
await writeFile(resolve(distDirectory, 'robots.txt'), [
  'User-agent: *',
  'Allow: /',
  '',
  `Sitemap: ${siteUrl}/sitemap.xml`,
  '',
].join('\n'));

console.log(`Activos públicos generados con ${experiences.length} experiencias aprobadas.`);
