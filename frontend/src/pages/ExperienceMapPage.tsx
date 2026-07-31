import axios from 'axios';
import { LocateFixed, MapPinned, Navigation } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import ExperienceMap from '../components/ExperienceMap';
import Skeleton from '../components/Skeleton';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import type { Experience } from '../types';

const formatPrice = (price: number) => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(price);

export const ExperienceMapPage = () => {
  const navigate = useNavigate();
  const [experiences, setExperiences] = useState<Experience[]>([]);
  const [userPoint, setUserPoint] = useState<{ latitude: number; longitude: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [locating, setLocating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    experienceService.getExperiences(controller.signal)
      .then((items) => {
        setExperiences(items.filter((item) => item.latitude !== null && item.longitude !== null));
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!axios.isCancel(requestError)) setError(toApiError(requestError).message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [retry]);

  const findNearby = () => {
    if (!navigator.geolocation) {
      setNotice('Este dispositivo no permite conocer tu ubicación.');
      return;
    }
    setLocating(true);
    setNotice(null);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const point = {
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        };
        setUserPoint(point);
        experienceService.getNearby(point.latitude, point.longitude, 50)
          .then((items) => {
            setExperiences(items);
            setNotice(items.length
              ? `Encontramos ${items.length} experiencia${items.length === 1 ? '' : 's'} a menos de 50 km.`
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
    navigate(`/experiences/${id}`, { state: { from: '/experiences/map' } });
  }, [navigate]);

  const points = experiences
    .filter((item) => item.latitude !== null && item.longitude !== null)
    .map((item) => ({
      id: item.id,
      title: item.title,
      latitude: item.latitude!,
      longitude: item.longitude!,
    }));

  return (
    <div className="container map-page animate-fade-in">
      <header className="page-heading map-page__heading">
        <div>
          <span className="page-heading__eyebrow">Explora por ubicación</span>
          <h1>Experiencias en el mapa</h1>
          <p>Descubre actividades con una ubicación confirmada por sus anfitriones.</p>
        </div>
        <Button onClick={findNearby} isLoading={locating}>
          <LocateFixed size={18} aria-hidden="true" /> Cerca de mí
        </Button>
      </header>
      {notice && <Alert tone="info">{notice}</Alert>}
      {loading ? <Skeleton className="map-page__loading" />
        : error ? <ErrorState description={error} onRetry={() => {
          setLoading(true);
          setRetry((current) => current + 1);
        }} />
        : (
          <div className="map-page__layout">
            <ExperienceMap
              points={points}
              userPoint={userPoint}
              onPointClick={openExperience}
              label="Mapa de experiencias disponibles"
            />
            {experiences.length === 0 ? (
              <EmptyState
                title="Sin experiencias ubicadas"
                description="Los anfitriones todavía no han señalado sus experiencias en el mapa."
                action={<Link className="button-link button-link--outline" to="/experiences">Ver catálogo</Link>}
              />
            ) : (
              <ol className="map-results">
                {experiences.map((experience) => (
                  <li key={experience.id}>
                    <Link to={`/experiences/${experience.id}`} state={{ from: '/experiences/map' }}>
                      <span className="map-results__icon"><MapPinned aria-hidden="true" /></span>
                      <span>
                        <strong>{experience.title}</strong>
                        <small>{experience.location}</small>
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
