import { LocateFixed, MapPinned, Navigation } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import ExperienceMap from '../components/ExperienceMap';
import Skeleton from '../components/Skeleton';
import ToastFeedback from '../components/ToastFeedback';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { formatLocationLabel } from '../services/googleMapsService';
import { experienceKeys, queryRefresh } from '../queries/queryKeys';
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

  const points = useMemo(() => experiences
    .filter((item) => item.latitude !== null && item.longitude !== null)
    .map((item) => ({
      id: item.id,
      title: item.title,
      latitude: item.latitude!,
      longitude: item.longitude!,
    })), [experiences]);
  const displayedExperiences = useMemo(() => focusedExperienceId
    ? [...experiences].sort((first, second) => (
      Number(second.id === focusedExperienceId) - Number(first.id === focusedExperienceId)
    ))
    : experiences, [experiences, focusedExperienceId]);

  return (
    <div className="container map-page animate-fade-in">
      <header className="page-heading map-page__heading">
        <div>
          <span className="page-heading__eyebrow">Explora por ubicación</span>
          <h1>Experiencias en el mapa</h1>
          <p>Encuentra experiencias cerca de ti o explora cada región.</p>
        </div>
        <Button onClick={findNearby} isLoading={locating}>
          <LocateFixed size={18} aria-hidden="true" /> Cerca de mí
        </Button>
      </header>
      <ToastFeedback message={notice} tone="info" />
      {loading ? <Skeleton className="map-page__loading" />
        : error ? <ErrorState description={error} onRetry={() => void mapQuery.refetch()} />
        : (
          <div className="map-page__layout">
            <ExperienceMap
              points={points}
              userPoint={userPoint}
              focusedPointId={focusedExperienceId}
              onPointClick={openExperience}
              label="Mapa de experiencias disponibles"
            />
            {experiences.length === 0 ? (
              <EmptyState
                title="Sin experiencias ubicadas"
                description="Todavía no hay experiencias para mostrar en el mapa."
                action={<Link className="button-link button-link--outline" to="/experiences">Ver catálogo</Link>}
              />
            ) : (
              <ol className="map-results">
                {displayedExperiences.map((experience) => (
                  <li key={experience.id}>
                    <Link
                      to={`/experiences/${experience.slug}`}
                      state={{ from: '/experiences/map' }}
                      aria-current={experience.id === focusedExperienceId ? 'location' : undefined}
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
                    </Link>
                  </li>
                ))}
              </ol>
            )}
          </div>
        )}
    </div>
  );
};

export default ExperienceMapPage;
