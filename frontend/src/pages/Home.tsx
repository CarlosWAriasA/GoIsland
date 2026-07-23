import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { ArrowRight, CalendarCheck, Search, TicketCheck } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import Button from '../components/Button';
import Card from '../components/Card';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import Skeleton from '../components/Skeleton';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import type { Experience } from '../types';

const FEATURED_LIMIT = 6;

const pickFeatured = (experiences: Experience[]) => [...experiences]
  .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  .slice(0, FEATURED_LIMIT);

const FeaturedSkeleton = () => (
  <div className="experience-grid" aria-hidden="true">
    {[1, 2, 3].map((item) => (
      <div className="surface-card experience-skeleton" key={item} aria-hidden="true">
        <Skeleton className="experience-skeleton__image" />
        <div className="experience-skeleton__body">
          <Skeleton className="experience-skeleton__line experience-skeleton__line--short" />
          <Skeleton className="experience-skeleton__line" />
          <Skeleton className="experience-skeleton__line" />
        </div>
      </div>
    ))}
  </div>
);

export const Home = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const [term, setTerm] = useState('');
  const [featured, setFeatured] = useState<Experience[]>([]);
  const [totalAvailable, setTotalAvailable] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const [completedRequest, setCompletedRequest] = useState<number | null>(null);
  const loading = completedRequest !== retryCount;

  useEffect(() => {
    const controller = new AbortController();
    experienceService.getExperiences(controller.signal)
      .then((data) => {
        setFeatured(pickFeatured(data));
        setTotalAvailable(data.length);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (axios.isCancel(requestError)) return;
        setFeatured([]);
        setTotalAvailable(0);
        setError(toApiError(requestError, 'No fue posible cargar las experiencias.').message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setCompletedRequest(retryCount);
      });
    return () => controller.abort();
  }, [retryCount]);

  useEffect(() => {
    if (!location.hash) return;
    document.getElementById(location.hash.slice(1))?.scrollIntoView({ block: 'start' });
  }, [location.hash]);

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const query = term.trim();
    navigate(query ? `/experiences?q=${encodeURIComponent(query)}` : '/experiences');
  };

  return (
    <div className="home-page animate-fade-in">
      <section className="experiences-hero home-hero" aria-labelledby="home-title">
        <div className="experiences-hero__content">
          <span className="experiences-hero__eyebrow">Bienvenido a GoIsland</span>
          <h1 id="home-title">La isla, contada por quienes viven en ella</h1>
          <p>
            Anfitriones dominicanos abren sus rutas, su mar y su cocina.
            Tú eliges la fecha; nosotros mostramos cupos y precios reales.
          </p>

          <form className="home-hero__search" onSubmit={submitSearch} role="search">
            <Input
              label="¿Qué quieres vivir?"
              placeholder="Ej. buceo, Samaná, gastronomía"
              value={term}
              onChange={(event) => setTerm(event.target.value)}
              maxLength={160}
            />
            <Button type="submit" variant="primary">
              <Search size={18} aria-hidden="true" />
              Buscar
            </Button>
          </form>

          {!loading && !error && totalAvailable > 0 && (
            <p className="home-hero__count">
              <strong>{totalAvailable}</strong>
              {totalAvailable === 1 ? ' experiencia disponible' : ' experiencias disponibles'} ahora mismo
            </p>
          )}
        </div>
      </section>

      <section className="container home-section" aria-labelledby="featured-title" aria-busy={loading}>
        <div className="home-section__heading">
          <h2 className="home-section__title" id="featured-title">Experiencias destacadas</h2>
          <Link className="home-section__link" to="/experiences">
            Ver todas las experiencias <ArrowRight size={17} aria-hidden="true" />
          </Link>
        </div>

        {loading ? (
          <FeaturedSkeleton />
        ) : error ? (
          <ErrorState
            title="No pudimos cargar las experiencias"
            description={error}
            onRetry={() => setRetryCount((current) => current + 1)}
          />
        ) : featured.length === 0 ? (
          <EmptyState
            title="Todavía no hay experiencias publicadas"
            description="Cuando los anfitriones publiquen actividades aprobadas, aparecerán aquí."
          />
        ) : (
          <div className="experience-grid">
            {featured.map((experience) => (
              <Card experience={experience} key={experience.id} />
            ))}
          </div>
        )}
      </section>

      <section className="container home-section" id="como-funciona" aria-labelledby="how-title">
        <h2 className="home-section__title" id="how-title">Cómo funciona</h2>
        <ol className="how-steps">
          <li className="surface-panel how-step">
            <span className="how-step__icon" aria-hidden="true"><Search size={22} /></span>
            <h3>1. Explora el catálogo</h3>
            <p>Filtra por nombre, ubicación, categoría y rango de precio para encontrar la actividad que buscas.</p>
          </li>
          <li className="surface-panel how-step">
            <span className="how-step__icon" aria-hidden="true"><CalendarCheck size={22} /></span>
            <h3>2. Elige fecha y cupos</h3>
            <p>Cada experiencia muestra sus horarios futuros y los cupos disponibles en cada uno.</p>
          </li>
          <li className="surface-panel how-step">
            <span className="how-step__icon" aria-hidden="true"><TicketCheck size={22} /></span>
            <h3>3. Reserva</h3>
            <p>Crea tu reserva con tu cuenta y sigue su estado desde “Mis reservas”.</p>
          </li>
        </ol>
      </section>

      <section className="container home-actions">
        <Link className="button-link button-link--primary" to="/experiences">Explorar experiencias</Link>
        <Link className="button-link button-link--outline" to="/host-profile">Quiero ser anfitrión</Link>
      </section>
    </div>
  );
};

export default Home;
