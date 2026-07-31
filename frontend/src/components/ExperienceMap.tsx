import { MapPin, Search, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { loadGoogleMaps } from '../services/googleMapsService';

export interface MapPoint {
  id: number | string;
  title: string;
  latitude: number;
  longitude: number;
}

export interface MapSelection {
  latitude: number;
  longitude: number;
  location: string;
}

interface ExperienceMapProps {
  points?: MapPoint[];
  selectedPoint?: { latitude: number; longitude: number } | null;
  userPoint?: { latitude: number; longitude: number } | null;
  onSelect?: (point: MapSelection) => void;
  onPointClick?: (id: MapPoint['id']) => void;
  searchEnabled?: boolean;
  searchValue?: string;
  label: string;
}

const defaultCenter = { lat: 18.7357, lng: -70.1627 };
const emptyPoints: MapPoint[] = [];

export const ExperienceMap = ({
  points = emptyPoints,
  selectedPoint,
  userPoint,
  onSelect,
  onPointClick,
  searchEnabled = false,
  searchValue = '',
  label,
}: ExperienceMapProps) => {
  const mapContainerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<google.maps.Map | null>(null);
  const autocompleteSessionRef = useRef<google.maps.places.AutocompleteSessionToken | null>(null);
  const onSelectRef = useRef(onSelect);
  const onPointClickRef = useRef(onPointClick);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [query, setQuery] = useState(searchValue);
  const [suggestions, setSuggestions] = useState<google.maps.places.PlacePrediction[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  useEffect(() => {
    onSelectRef.current = onSelect;
    onPointClickRef.current = onPointClick;
  }, [onPointClick, onSelect]);

  useEffect(() => {
    if (!searchEnabled || query.trim().length < 2 || query === searchValue) {
      return undefined;
    }

    let cancelled = false;
    const timeout = window.setTimeout(async () => {
      setIsSearching(true);
      setSearchError(null);
      try {
        const { places } = await loadGoogleMaps();
        autocompleteSessionRef.current ??= new places.AutocompleteSessionToken();
        const response = await places.AutocompleteSuggestion.fetchAutocompleteSuggestions({
          input: query.trim(),
          language: 'es',
          region: 'do',
          includedRegionCodes: ['do'],
          locationBias: { center: defaultCenter, radius: 50_000 },
          sessionToken: autocompleteSessionRef.current,
        });
        if (!cancelled) {
          setSuggestions(
            response.suggestions
              .map((suggestion) => suggestion.placePrediction)
              .filter((prediction): prediction is google.maps.places.PlacePrediction => Boolean(prediction)),
          );
        }
      } catch {
        if (!cancelled) {
          setSuggestions([]);
          setSearchError('No se pudieron cargar los lugares.');
        }
      } finally {
        if (!cancelled) setIsSearching(false);
      }
    }, 250);

    return () => {
      cancelled = true;
      window.clearTimeout(timeout);
    };
  }, [query, searchEnabled, searchValue]);

  const selectSuggestion = async (prediction: google.maps.places.PlacePrediction) => {
    setIsSearching(true);
    setSearchError(null);
    try {
      const place = prediction.toPlace();
      await place.fetchFields({
        fields: ['displayName', 'formattedAddress', 'location', 'viewport'],
      });
      if (!place.location) return;

      const latitude = Number(place.location.lat().toFixed(6));
      const longitude = Number(place.location.lng().toFixed(6));
      const location = place.formattedAddress || place.displayName || prediction.text.text;
      setQuery(location);
      setSuggestions([]);
      autocompleteSessionRef.current = null;

      if (place.viewport) {
        mapRef.current?.fitBounds(place.viewport);
      } else {
        mapRef.current?.setCenter(place.location);
        mapRef.current?.setZoom(16);
      }
      onSelectRef.current?.({ latitude, longitude, location });
    } catch {
      setSearchError('No se pudo seleccionar el lugar.');
    } finally {
      setIsSearching(false);
    }
  };

  useEffect(() => {
    const mapContainer = mapContainerRef.current;
    if (!mapContainer) return undefined;
    let cancelled = false;
    const markers: google.maps.Marker[] = [];
    let userCircle: google.maps.Circle | null = null;
    let clickListener: google.maps.MapsEventListener | null = null;

    const initialize = async () => {
      try {
        const { maps, geocoding } = await loadGoogleMaps();
        if (cancelled) return;

        const map = new maps.Map(mapContainer, {
          center: defaultCenter,
          zoom: 8,
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: true,
          clickableIcons: false,
        });
        mapRef.current = map;
        const bounds = new google.maps.LatLngBounds();
        let boundsCount = 0;

        points.forEach((point) => {
          const position = { lat: point.latitude, lng: point.longitude };
          bounds.extend(position);
          boundsCount += 1;
          const marker = new google.maps.Marker({
            map,
            position,
            title: point.title,
          });
          marker.addListener('click', () => onPointClickRef.current?.(point.id));
          markers.push(marker);
        });

        if (selectedPoint) {
          const position = { lat: selectedPoint.latitude, lng: selectedPoint.longitude };
          bounds.extend(position);
          boundsCount += 1;
          markers.push(new google.maps.Marker({
            map,
            position,
            title: 'Ubicación seleccionada',
            animation: google.maps.Animation.DROP,
          }));
        }

        if (userPoint) {
          const position = { lat: userPoint.latitude, lng: userPoint.longitude };
          bounds.extend(position);
          boundsCount += 1;
          userCircle = new google.maps.Circle({
            map,
            center: position,
            radius: 350,
            fillColor: '#1C6FA5',
            fillOpacity: 0.2,
            strokeColor: '#1C6FA5',
            strokeOpacity: 0.9,
            strokeWeight: 2,
          });
        }

        if (boundsCount === 1) {
          map.setCenter(bounds.getCenter());
          map.setZoom(15);
        } else if (boundsCount > 1) {
          map.fitBounds(bounds, 48);
        }

        if (onSelectRef.current) {
          const geocoder = new geocoding.Geocoder();
          clickListener = map.addListener('click', async (event: google.maps.MapMouseEvent) => {
            if (!event.latLng) return;
            const latitude = Number(event.latLng.lat().toFixed(6));
            const longitude = Number(event.latLng.lng().toFixed(6));
            let location = `${latitude.toFixed(5)}, ${longitude.toFixed(5)}`;
            try {
              const response = await geocoder.geocode({ location: event.latLng });
              location = response.results[0]?.formatted_address || location;
            } catch {
              // Las coordenadas permiten conservar la selección si la geocodificación falla.
            }
            setQuery(location);
            setSuggestions([]);
            setSearchError(null);
            onSelectRef.current?.({ latitude, longitude, location });
          });
        }

        setLoadError(null);
      } catch {
        if (!cancelled) setLoadError('Google Maps no está disponible.');
      }
    };

    void initialize();
    return () => {
      cancelled = true;
      clickListener?.remove();
      markers.forEach((marker) => marker.setMap(null));
      userCircle?.setMap(null);
      mapRef.current = null;
    };
  }, [points, selectedPoint, userPoint]);

  return (
    <div className="google-map-shell">
      {searchEnabled && !loadError && (
        <div className="google-map-autocomplete">
          <div className="google-map-search">
            <Search size={19} aria-hidden="true" />
            <input
              type="text"
              value={query}
              onChange={(event) => {
                const value = event.target.value;
                setQuery(value);
                setSearchError(null);
                if (value.trim().length < 2 || value === searchValue) {
                  setSuggestions([]);
                  setIsSearching(false);
                }
              }}
              placeholder="Buscar un lugar"
              aria-label="Buscar un lugar"
              aria-autocomplete="list"
              aria-expanded={suggestions.length > 0}
              aria-controls="google-map-suggestions"
              autoComplete="off"
            />
            {query && (
              <button
                type="button"
                className="google-map-search__clear"
                onClick={() => {
                  setQuery('');
                  setSuggestions([]);
                  setSearchError(null);
                }}
                aria-label="Limpiar búsqueda"
              >
                <X size={18} />
              </button>
            )}
          </div>
          {(suggestions.length > 0 || isSearching || searchError) && (
            <div
              id="google-map-suggestions"
              className="google-map-suggestions"
              role="listbox"
            >
              {isSearching && suggestions.length === 0 && (
                <div className="google-map-suggestions__status">Buscando…</div>
              )}
              {searchError && (
                <div className="google-map-suggestions__status google-map-suggestions__status--error">
                  {searchError}
                </div>
              )}
              {suggestions.map((prediction) => (
                <button
                  key={prediction.placeId}
                  type="button"
                  className="google-map-suggestion"
                  role="option"
                  aria-selected="false"
                  onClick={() => void selectSuggestion(prediction)}
                >
                  <MapPin size={18} aria-hidden="true" />
                  <span>
                    <strong>{prediction.mainText?.text || prediction.text.text}</strong>
                    {prediction.secondaryText?.text && <small>{prediction.secondaryText.text}</small>}
                  </span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}
      {loadError && <div className="google-map-error" role="alert">{loadError}</div>}
      <div
        className="experience-map"
        ref={mapContainerRef}
        role="application"
        aria-label={label}
        hidden={Boolean(loadError)}
      />
    </div>
  );
};

export default ExperienceMap;
