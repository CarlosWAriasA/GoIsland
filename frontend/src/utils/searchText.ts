/**
 * Normalización de texto para búsquedas locales: sin diacríticos y en minúsculas.
 * Replica el comportamiento de `goisland_normalize` en el servidor, de modo que
 * "samana" encuentre "Samaná" y "SAMANÁ" encuentre "samana".
 */
export const normalizeSearchText = (value: string): string => value
  .normalize('NFD')
  .replace(/\p{Diacritic}/gu, '')
  .normalize('NFC')
  .toLowerCase();

/** Indica si `haystack` contiene `normalizedNeedle`, ignorando tildes y mayúsculas. */
export const matchesSearch = (
  haystack: string | null | undefined,
  normalizedNeedle: string,
): boolean => Boolean(haystack) && normalizeSearchText(haystack!).includes(normalizedNeedle);
