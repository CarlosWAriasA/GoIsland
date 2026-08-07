export const getReturnPath = (state: unknown, fallback: string): string => {
  const from = (state as { from?: unknown } | null)?.from;
  return typeof from === 'string' && from.startsWith('/') && !from.startsWith('//')
    ? from
    : fallback;
};

export const buildGoogleMapsUrl = (
  latitude: number | null | undefined,
  longitude: number | null | undefined,
  fallbackQuery: string,
): string => {
  const query = typeof latitude === 'number' && typeof longitude === 'number'
    ? `${latitude},${longitude}`
    : encodeURIComponent(fallbackQuery);
  return `https://www.google.com/maps/search/?api=1&query=${query}`;
};
