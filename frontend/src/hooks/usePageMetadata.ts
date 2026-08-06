import { useEffect } from 'react';

const DEFAULT_TITLE = 'GoIsland - Experiencias y actividades en República Dominicana';
const DEFAULT_DESCRIPTION = 'Descubre experiencias locales, actividades y recorridos para disfrutar República Dominicana.';

interface PageMetadata {
  title: string;
  description: string;
  path: string;
  image?: string;
  type?: 'website' | 'article';
  structuredData?: Record<string, unknown>;
}

const getSiteOrigin = () => (
  import.meta.env.VITE_SITE_URL || window.location.origin
).replace(/\/$/, '');

const setMeta = (selector: string, attribute: 'name' | 'property', key: string, content: string) => {
  let element = document.head.querySelector<HTMLMetaElement>(selector);
  if (!element) {
    element = document.createElement('meta');
    element.setAttribute(attribute, key);
    document.head.appendChild(element);
  }
  element.content = content;
};

const setCanonical = (href: string) => {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  if (!element) {
    element = document.createElement('link');
    element.rel = 'canonical';
    document.head.appendChild(element);
  }
  element.href = href;
};

const applyDefaultMetadata = () => {
  const canonical = `${getSiteOrigin()}${window.location.pathname}`;
  document.title = DEFAULT_TITLE;
  setMeta('meta[name="description"]', 'name', 'description', DEFAULT_DESCRIPTION);
  setMeta('meta[property="og:title"]', 'property', 'og:title', DEFAULT_TITLE);
  setMeta('meta[property="og:description"]', 'property', 'og:description', DEFAULT_DESCRIPTION);
  setMeta('meta[property="og:type"]', 'property', 'og:type', 'website');
  setMeta('meta[property="og:url"]', 'property', 'og:url', canonical);
  setMeta('meta[name="twitter:card"]', 'name', 'twitter:card', 'summary');
  setMeta('meta[name="twitter:title"]', 'name', 'twitter:title', DEFAULT_TITLE);
  setMeta('meta[name="twitter:description"]', 'name', 'twitter:description', DEFAULT_DESCRIPTION);
  setCanonical(canonical);
  document.getElementById('page-structured-data')?.remove();
};

export const usePageMetadata = (metadata?: PageMetadata) => {
  useEffect(() => {
    if (!metadata) {
      applyDefaultMetadata();
      return;
    }

    const canonical = `${getSiteOrigin()}${metadata.path.startsWith('/') ? '' : '/'}${metadata.path}`;
    const image = metadata.image
      ? new URL(metadata.image, getSiteOrigin()).toString()
      : undefined;

    document.title = metadata.title;
    setMeta('meta[name="description"]', 'name', 'description', metadata.description);
    setMeta('meta[property="og:title"]', 'property', 'og:title', metadata.title);
    setMeta('meta[property="og:description"]', 'property', 'og:description', metadata.description);
    setMeta('meta[property="og:type"]', 'property', 'og:type', metadata.type ?? 'website');
    setMeta('meta[property="og:url"]', 'property', 'og:url', canonical);
    setMeta('meta[name="twitter:title"]', 'name', 'twitter:title', metadata.title);
    setMeta('meta[name="twitter:description"]', 'name', 'twitter:description', metadata.description);
    setCanonical(canonical);

    if (image) {
      setMeta('meta[property="og:image"]', 'property', 'og:image', image);
      setMeta('meta[name="twitter:image"]', 'name', 'twitter:image', image);
      setMeta('meta[name="twitter:card"]', 'name', 'twitter:card', 'summary_large_image');
    } else {
      document.head.querySelector('meta[property="og:image"]')?.remove();
      document.head.querySelector('meta[name="twitter:image"]')?.remove();
      setMeta('meta[name="twitter:card"]', 'name', 'twitter:card', 'summary');
    }

    document.getElementById('page-structured-data')?.remove();
    if (metadata.structuredData) {
      const script = document.createElement('script');
      script.id = 'page-structured-data';
      script.type = 'application/ld+json';
      script.text = JSON.stringify(metadata.structuredData);
      document.head.appendChild(script);
    }

    return applyDefaultMetadata;
  }, [metadata]);
};

