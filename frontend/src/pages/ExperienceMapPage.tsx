import { LocateFixed, MapPinned, Navigation, RotateCcw, Search } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import ExperienceMap from '../components/ExperienceMap';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import ToastFeedback from '../components/ToastFeedback';
import { toApiError } from '../services/apiError';
import { resolveApiAssetUrl } from '../services/api';
import { experienceService } from '../services/experienceService';
import { formatLocationLabel } from '../services/googleMapsService';
import { experienceKeys, queryRefresh } from '../queries/queryKeys';
import { matchesSearch, normalizeSearchText } from '../utils/searchText';
import type { Experience } from '../types';

const formatPrice = (price: number) => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(price);

export const ExperienceMapPage = () => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedExperienceId = Number(searchParams.get('experience'));
  const focusedExperienceId = Number.isInteger(requestedExperienceId) && requestedExperienceId > 0
    ? requestedExperienceId
    : undefined;

  const [selectedExperienceId, setSelectedExperienceId] = useState<number | string | undefined>(focusedExperienceId);
  const activeFocusedId = selectedExperienceId ?? focusedExperienceId;

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [maxPrice, setMaxPrice] = useState('');

  const mapQuery = useQuery({
    queryKey: experienceKeys.map(),
    queryFn: ({ signal }) => experienceService.getExperiences(signal, { pageSize: 100 }),
    refetchInterval: queryRefresh.catalog,
    refetchOnMount: 'always',
  });

  const catalogExperiences = useMemo(() => mapQuery.data?.items.filter(
    (item) => item.latitude !== null && item.longitude !== null,
  ) ?? [], [mapQuery.data]);

  const [nearbyExperiences, setNearbyExperiences] = useState<Experience[] | null>(null);
  const experiences = nearbyExperiences ?? catalogExperiences;
  const [userPoint, setUserPoint] = useState<{ latitude: number; longitude: number } | null>(null);
  const [locating, setLocating] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const loading = mapQuery.isPending;
  const error = !mapQuery.data && mapQuery.error ? toApiError(mapQuery.error).message : null;

  const categories = useMemo(() => {
    const set = new Set<string>();
    catalogExperiences.forEach((exp) => {
      if (exp.category) set.add(exp.category);
    });
    return Array.from(set).sort();
  }, [catalogExperiences]);

  const filteredExperiences = useMemo(() => {
    return experiences.filter((item) => {
      if (item.latitude === null || item.longitude === null) return false;

      if (searchTerm.trim()) {
        const query = normalizeSearchText(searchTerm.trim());
        const matchesTitle = matchesSearch(item.title, query);
        const matchesLoc = matchesSearch(item.location, query);
        const matchesCat = matchesSearch(item.category, query);
        if (!matchesTitle && !matchesLoc && !matchesCat) return false;
      }

      if (selectedCategory && item.category !== selectedCategory) {
        return false;
      }

      if (maxPrice && Number(maxPrice) > 0 && item.price > Number(maxPrice)) {
        return false;
      }

      return true;
    });
  }, [experiences, searchTerm, selectedCategory, maxPrice]);

  const handleResetAll = useCallback(() => {
    setSearchTerm('');
    setSelectedCategory('');
    setMaxPrice('');
    setNearbyExperiences(null);
    setUserPoint(null);
    setNotice(null);
    setSelectedExperienceId(undefined);
    setSearchParams(new URLSearchParams(), { replace: true });
  }, [setSearchParams]);

  const findNearby = () => {
    if (!navigator.geolocation) {
      setNotice('Este dispositivo no permite conocer tu ubicación.');
      return;
    }
    setLocating(true);
    setNotice(null);
    setSearchParams(new URLSearchParams(), { replace: true });
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const point = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        };
        setUserPoint(point);
        experienceService.getNearby(point.latitude, point.longitude, 50)
          .then((page) => {
            setNearbyExperiences(page.items);
            setNotice(page.totalItems
              ? `Encontramos ${page.totalItems} experiencia${page.totalItems === 1 ? '' : 's'} a menos de 50 km.`
              : 'No encontramos experiencias a menos de 50 km. Puedes explorar el mapa completo.');
          })
          .catch((requestError: unknown) => setNotice(toApiError(
            requestError,
            'No fue posible buscar experiencias cercanas.',
          ).message))
          .finally(() => setLocating(false));
      },
      () => {
        setNotice('No pudimos usar tu ubicación. Revisa el permiso del sitio e inténtalo nuevamente.');
        setLocating(false);
      },
      { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 },
    );
  };

  const openExperience = useCallback((id: string | number) => {
    const experience = experiences.find((item) => item.id === Number(id));
    navigate(`/experiences/${experience?.slug ?? id}`, { state: { from: '/experiences/map' } });
  }, [experiences, navigate]);

  const points = useMemo(() => filteredExperiences
    .map((item) => {
      const coverImage = item.images?.find((img) => img.isCover) ?? item.images?.[0];
      return {
        id: item.id,
        title: item.title,
        latitude: item.latitude!,
        longitude: item.longitude!,
        slug: item.slug,
        category: item.category,
        price: item.price,
        location: item.location,
        coverImageUrl: coverImage ? resolveApiAssetUrl(coverImage.url) : undefined,
      };
    }), [filteredExperiences]);

  const displayedExperiences = useMemo(() => activeFocusedId
    ? [...filteredExperiences].sort((first, second) => (
      Number(second.id === activeFocusedId) - Number(first.id === activeFocusedId)
    ))
    : filteredExperiences, [filteredExperiences, activeFocusedId]);

  const hasActiveFilters = Boolean(
    searchTerm || selectedCategory || maxPrice || nearbyExperiences !== null || userPoint !== null || notice !== null,
  );

  return (
    <div className="container map-page animate-fade-in">
      <header className="page-heading map-page__heading">
        <div>
          <span className="page-heading__eyebrow">Explora por ubicación</span>
          <h1>Experiencias en el mapa</h1>
          <p>Encuentra experiencias cerca de ti o explora cada región.</p>
        </div>
        <div className="map-page__actions">
          <Button onClick={findNearby} isLoading={locating}>
            <LocateFixed size={18} aria-hidden="true" /> Cerca de mí
          </Button>
        </div>
      </header>

      <ToastFeedback message={notice} tone="info" />

      {!loading && !error && (
        <div
          className={`map-page__filters surface-panel${hasActiveFilters ? ' map-page__filters--with-reset' : ''}`}
        >
          <div className="map-page__search-col">
            <Input
              label="Buscar"
              icon={<Search size={18} />}
              placeholder="Buscar por nombre, categoría o ubicación..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          <div className="map-page__filter-col">
            <SelectField
              label="Categoría"
              value={selectedCategory}
              onChange={(e) => setSelectedCategory(e.target.value)}
            >
              <option value="">Todas las categorías</option>
              {categories.map((cat) => (
                <option key={cat} value={cat}>{cat}</option>
              ))}
            </SelectField>
          </div>

          <div className="map-page__filter-col">
            <SelectField
              label="Precio máximo"
              value={maxPrice}
              onChange={(e) => setMaxPrice(e.target.value)}
            >
              <option value="">Cualquier precio</option>
              <option value="25">Hasta $25 USD</option>
              <option value="50">Hasta $50 USD</option>
              <option value="100">Hasta $100 USD</option>
              <option value="200">Hasta $200 USD</option>
            </SelectField>
          </div>

          {hasActiveFilters && (
            <div className="map-page__reset-col">
              <Button
                variant="ghost"
                size="sm"
                onClick={handleResetAll}
              >
                <RotateCcw size={15} aria-hidden="true" /> Limpiar filtros
              </Button>
            </div>
          )}
        </div>
      )}

      {loading ? <Skeleton className="map-page__loading" />
        : error ? <ErrorState description={error} onRetry={() => void mapQuery.refetch()} />
        : (
          <div className="map-page__layout">
            <ExperienceMap
              points={points}
              userPoint={userPoint}
              focusedPointId={activeFocusedId}
              showInfoWindow={true}
              onPointClick={openExperience}
              label="Mapa de experiencias disponibles"
            />
            <div className="map-page__results">
              {filteredExperiences.length === 0 ? (
                <EmptyState
                  title="Sin experiencias encontradas"
                  description={hasActiveFilters ? "No hay experiencias que coincidan con tus criterios." : "Todavía no hay experiencias para mostrar en el mapa."}
                  action={hasActiveFilters ? (
                    <Button
                      variant="outline"
                      onClick={handleResetAll}
                    >
                      Ver todas las experiencias
                    </Button>
                  ) : (
                    <Link className="button-link button-link--outline" to="/experiences">Ver catálogo</Link>
                  )}
                />
              ) : (
                <>
                  <p className="map-results__summary" role="status">
                    <strong>{filteredExperiences.length}</strong>{' '}
                    <span>
                      {filteredExperiences.length === 1
                        ? 'experiencia disponible'
                        : 'experiencias disponibles'}
                    </span>{' '}
                    {filteredExperiences.length !== catalogExperiences.length && (
                      <small>de {catalogExperiences.length} en el catálogo</small>
                    )}
                  </p>
                  <ol className="map-results">
                    {displayedExperiences.map((experience) => {
                      const isSelected = String(experience.id) === String(activeFocusedId ?? '');
                      return (
                        <li key={experience.id}>
                          <button
                            type="button"
                            className={`map-results__button ${isSelected ? 'is-active' : ''}`}
                            onClick={() => setSelectedExperienceId(experience.id)}
                            aria-current={isSelected ? 'location' : undefined}
                          >
                            <span className="map-results__icon"><MapPinned aria-hidden="true" /></span>
                            <span>
                              <strong>{experience.title}</strong>
                              <small>{formatLocationLabel(experience.location)}</small>
                              {experience.distanceKm !== null && (
                                <small><Navigation size={14} aria-hidden="true" /> A {experience.distanceKm.toFixed(1)} km</small>
                              )}
                            </span>
                            <b>{formatPrice(experience.price)}</b>
                          </button>
                        </li>
                      );
                    })}
                  </ol>
                </>
              )}
            </div>
          </div>
        )}
    </div>
  );
};

export default ExperienceMapPage;
