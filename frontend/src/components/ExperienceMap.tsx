import { MapPin, Search, X } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { formatLocationLabel, loadGoogleMaps } from '../services/googleMapsService';

export interface MapPoint {
  id: number | string;
  title: string;
  latitude: number;
  longitude: number;
  slug?: string;
  category?: string;
  price?: number;
  location?: string;
  coverImageUrl?: string;
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
  focusedPointId?: MapPoint['id'];
  showInfoWindow?: boolean;
  searchEnabled?: boolean;
  searchValue?: string;
  label: string;
}

const defaultCenter = { lat: 18.7357, lng: -70.1627 };
const emptyPoints: MapPoint[] = [];

const createInfoWindowContent = (point: MapPoint, onPointClick?: (id: MapPoint['id']) => void): HTMLElement => {
  const container = document.createElement('div');
  container.className = 'map-info-window';

  const priceLabel = point.price === 0
    ? 'Gratis'
    : point.price !== undefined
      ? `$${point.price.toLocaleString('es-DO')} USD`
      : '';

  const imageUrl = point.coverImageUrl;

  container.innerHTML = `
    ${imageUrl ? `<div class="map-info-window__image" style="background-image: url('${imageUrl}')"></div>` : ''}
    <div class="map-info-window__body">
      ${point.category ? `<span class="map-info-window__badge">${point.category}</span>` : ''}
      <h3 class="map-info-window__title">${point.title}</h3>
      ${point.location ? `<p class="map-info-window__location">${point.location}</p>` : ''}
      <div class="map-info-window__footer">
        ${priceLabel ? `<span class="map-info-window__price">${priceLabel}</span>` : '<span></span>'}
        <button type="button" class="map-info-window__button">Ver detalles</button>
      </div>
    </div>
  `;

  const btn = container.querySelector('.map-info-window__button');
  if (btn) {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      onPointClick?.(point.id);
    });
  }

  return container;
};

export const ExperienceMap = ({
  points = emptyPoints,
  selectedPoint,
  userPoint,
  onSelect,
  onPointClick,
  focusedPointId,
  showInfoWindow = false,
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
  const [queryDraft, setQueryDraft] = useState({ source: searchValue, value: searchValue });
  const query = queryDraft.source === searchValue ? queryDraft.value : searchValue;
  const setQuery = useCallback(
    (value: string) => setQueryDraft({ source: searchValue, value }),
    [searchValue],
  );
  const [suggestions, setSuggestions] = useState<google.maps.places.PlacePrediction[]>([]);
  const [activeIndex, setActiveIndex] = useState<number>(-1);
  const [, setIsSearching] = useState(false);
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
          const nextSuggestions = response.suggestions
            .map((suggestion) => suggestion.placePrediction)
            .filter((prediction): prediction is google.maps.places.PlacePrediction => Boolean(prediction));
          setSuggestions(nextSuggestions);
          setActiveIndex(nextSuggestions.length > 0 ? 0 : -1);
        }
      } catch {
        if (!cancelled) {
          setSuggestions([]);
          setActiveIndex(-1);
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
      const location = formatLocationLabel(
        place.formattedAddress || place.displayName || prediction.text.text,
      );
      setQuery(location);
      setSuggestions([]);
      setActiveIndex(-1);
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

  const pointsKey = points.map((p) => `${p.id}:${p.latitude}:${p.longitude}`).join('|');
  const selectedPointKey = selectedPoint ? `${selectedPoint.latitude}:${selectedPoint.longitude}` : '';
  const userPointKey = userPoint ? `${userPoint.latitude}:${userPoint.longitude}` : '';

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
          gestureHandling: 'greedy',
        });
        mapRef.current = map;
        const bounds = new google.maps.LatLngBounds();
        let boundsCount = 0;
        let focusedPosition: google.maps.LatLngLiteral | null = null;

        const infoWindow = showInfoWindow ? new maps.InfoWindow({
          maxWidth: 290,
          disableAutoPan: false,
        }) : null;

        points.forEach((point) => {
          const position = { lat: point.latitude, lng: point.longitude };
          const isFocused = String(point.id) === String(focusedPointId ?? '');
          if (isFocused) focusedPosition = position;
          bounds.extend(position);
          boundsCount += 1;
          const marker = new google.maps.Marker({
            map,
            position,
            title: point.title,
            animation: isFocused ? google.maps.Animation.DROP : undefined,
            zIndex: isFocused ? 10 : undefined,
          });

          const openPopup = () => {
            map.setCenter(position);
            map.setZoom(15);
            if (showInfoWindow && infoWindow) {
              infoWindow.setContent(createInfoWindowContent(point, (id) => onPointClickRef.current?.(id)));
              infoWindow.open(map, marker);
            }
          };

          marker.addListener('click', openPopup);
          markers.push(marker);

          if (isFocused && showInfoWindow) {
            openPopup();
          }
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

        if (focusedPosition) {
          map.setCenter(focusedPosition);
          map.setZoom(15);
        } else if (boundsCount === 1) {
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
              location = formatLocationLabel(response.results[0]?.formatted_address || location);
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
  }, [focusedPointId, pointsKey, selectedPointKey, userPointKey, showInfoWindow, setQuery]);

  return (
    <div className="experience-map">
      {searchEnabled && (
        <div className="experience-map__search">
          <div className="experience-map__search-input">
            <Search size={18} aria-hidden="true" />
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'ArrowDown') {
                  e.preventDefault();
                  setActiveIndex((prev) => (prev < suggestions.length - 1 ? prev + 1 : 0));
                } else if (e.key === 'ArrowUp') {
                  e.preventDefault();
                  setActiveIndex((prev) => (prev > 0 ? prev - 1 : suggestions.length - 1));
                } else if (e.key === 'Enter' && activeIndex >= 0 && suggestions[activeIndex]) {
                  e.preventDefault();
                  void selectSuggestion(suggestions[activeIndex]);
                } else if (e.key === 'Escape') {
                  setSuggestions([]);
                }
              }}
              placeholder="Buscar un lugar en República Dominicana..."
              aria-label="Buscar lugar en el mapa"
            />
            {query && (
              <button
                type="button"
                className="experience-map__search-clear"
                onClick={() => {
                  setQuery('');
                  setSuggestions([]);
                }}
                aria-label="Limpiar búsqueda"
              >
                <X size={16} />
              </button>
            )}
          </div>
          {suggestions.length > 0 && (
            <ul className="experience-map__suggestions" role="listbox">
              {suggestions.map((suggestion, index) => (
                <li
                  key={suggestion.placeId}
                  className={`experience-map__suggestion${index === activeIndex ? ' is-active' : ''}`}
                  onClick={() => void selectSuggestion(suggestion)}
                  role="option"
                  aria-selected={index === activeIndex}
                >
                  <MapPin size={16} aria-hidden="true" />
                  <span>{suggestion.text.text}</span>
                </li>
              ))}
            </ul>
          )}
          {searchError && <p className="experience-map__search-error">{searchError}</p>}
        </div>
      )}
      {loadError ? (
        <div className="experience-map__error">
          <p>{loadError}</p>
        </div>
      ) : (
        <div
          ref={mapContainerRef}
          className="experience-map__canvas"
          role="region"
          aria-label={label}
        />
      )}
    </div>
  );
};

export default ExperienceMap;
