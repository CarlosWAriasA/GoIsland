import { importLibrary, setOptions } from '@googlemaps/js-api-loader';
import { api } from './api';

interface PublicConfiguration {
  googleMapsApiKey: string;
}

let configurationPromise: Promise<PublicConfiguration> | null = null;
let loaderConfigured = false;

const getConfiguration = () => {
  configurationPromise ??= api.get<PublicConfiguration>('/config/public')
    .then((response) => response.data);
  return configurationPromise;
};

const configureLoader = async () => {
  if (loaderConfigured) return;
  const configuration = await getConfiguration();
  if (!configuration.googleMapsApiKey.trim()) {
    throw new Error('Google Maps no está configurado.');
  }

  setOptions({
    key: configuration.googleMapsApiKey,
    v: 'weekly',
    language: 'es',
    region: 'DO',
    authReferrerPolicy: 'origin',
  });
  loaderConfigured = true;
};

export const loadGoogleMaps = async () => {
  await configureLoader();
  const [maps, places, geocoding] = await Promise.all([
    importLibrary('maps'),
    importLibrary('places'),
    importLibrary('geocoding'),
  ]);
  return { maps, places, geocoding };
};

export const reverseGeocodeLocation = async (latitude: number, longitude: number) => {
  const { geocoding } = await loadGoogleMaps();
  const response = await new geocoding.Geocoder().geocode({
    location: { lat: latitude, lng: longitude },
  });
  return response.results[0]?.formatted_address ?? '';
};
