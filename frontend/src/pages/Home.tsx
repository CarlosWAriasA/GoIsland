import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowRight,
  BadgeCheck,
  CalendarCheck,
  MapPinned,
  MessageSquareQuote,
  Search,
  ShieldCheck,
  TicketCheck,
} from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import Button from '../components/Button';
import Card from '../components/Card';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import Skeleton from '../components/Skeleton';
import Typewriter from '../components/Typewriter';
import { useRevealOnScroll } from '../hooks/useRevealOnScroll';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import { formatLocationLabel } from '../services/googleMapsService';
import { experienceKeys, queryRefresh } from '../queries/queryKeys';
import type { Experience } from '../types';

const FEATURED_LIMIT = 6;

const categoryTiles = [
  { label: 'Acuático', photo: 'acuatico', hint: 'Buceo, snorkel y navegación' },
  { label: 'Naturaleza', photo: 'naturaleza', hint: 'Cascadas, montañas y senderos' },
  { label: 'Gastronomía', photo: 'gastronomia', hint: 'Cocina criolla y mercados' },
  { label: 'Aventura', photo: 'playa', hint: 'Rutas activas al aire libre' },
  { label: 'Arte y cultura', photo: 'ciudad', hint: 'Historia, música y barrios' },
  { label: 'Cruceros', photo: 'cruceros', hint: 'Salidas en barco y bahías' },
] as const;

const DESTINATIONS_LIMIT = 6;

const pickDestinations = (experiences: Experience[]) => {
  const counters = new Map<string, number>();
  experiences.forEach((experience) => {
    const parts = formatLocationLabel(experience.location).split(',');
    const zone = parts[parts.length - 1].trim();
    if (!zone) return;
    counters.set(zone, (counters.get(zone) ?? 0) + 1);
  });
  return Array.from(counters.entries())
    .sort((first, second) => second[1] - first[1] || first[0].localeCompare(second[0], 'es'))
    .slice(0, DESTINATIONS_LIMIT)
    .map(([zone]) => zone);
};

const trustPoints = [
  {
    icon: ShieldCheck,
    title: 'Pago protegido',
    text: 'Los cobros se procesan con Stripe. Tus datos de tarjeta nunca pasan por GoIsland.',
  },
  {
    icon: BadgeCheck,
    title: 'Anfitriones revisados',
    text: 'Cada perfil que publica pasa por una verificación antes de aparecer en el catálogo.',
  },
  {
    icon: MessageSquareQuote,
    title: 'Reseñas de quien fue',
    text: 'Solo puede opinar quien completó la reserva. Nada de valoraciones inventadas.',
  },
  {
    icon: CalendarCheck,
    title: 'Cambios sin llamadas',
    text: 'Pide otra fecha o la cancelación desde la reserva y sigue la respuesta del anfitrión.',
  },
] as const;

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
  const featuredQuery = useQuery({
    queryKey: experienceKeys.featured(),
    queryFn: ({ signal }) => experienceService.getExperiences(signal, {
      pageSize: FEATURED_LIMIT,
      sort: 'newest',
    }),
    refetchInterval: queryRefresh.catalog,
    refetchOnMount: 'always',
  });
  const destinationsQuery = useQuery({
    queryKey: experienceKeys.map(),
    queryFn: ({ signal }) => experienceService.getExperiences(signal, { pageSize: 100 }),
    refetchInterval: queryRefresh.catalog,
  });
  const destinations = pickDestinations(destinationsQuery.data?.items ?? []);
  const featured = pickFeatured(featuredQuery.data?.items ?? []);
  const totalAvailable = featuredQuery.data?.totalItems ?? 0;
  const error = !featuredQuery.data && featuredQuery.error
    ? toApiError(featuredQuery.error, 'No fue posible cargar las experiencias.').message
    : null;
  const loading = featuredQuery.isPending;

  useRevealOnScroll(!loading);

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
          <h1 id="home-title">
            <Typewriter text="La isla, contada por quienes viven en ella" />
          </h1>
          <p>
            Anfitriones dominicanos abren sus rutas, su mar y su cocina.
            Elige una experiencia y consulta sus fechas antes de reservar.
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

          {destinations.length > 0 && (
            <div className="home-hero__suggestions">
              <span className="home-hero__suggestions-label">Destinos con actividad</span>
              <ul>
                {destinations.map((place) => (
                  <li key={place}>
                    <Link to={`/experiences?location=${encodeURIComponent(place)}`}>{place}</Link>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {!loading && !error && totalAvailable > 0 && (
            <p className="home-hero__count">
              <strong>{totalAvailable}</strong>
              {totalAvailable === 1 ? ' experiencia disponible' : ' experiencias disponibles'} ahora mismo
            </p>
          )}
        </div>
      </section>

      <section className="container home-section" data-reveal aria-labelledby="categories-title">
        <div className="home-section__heading">
          <h2 className="home-section__title" id="categories-title">Explora por categoría</h2>
          <Link className="home-section__link" to="/experiences">
            Ver el catálogo completo <ArrowRight size={17} aria-hidden="true" />
          </Link>
        </div>

        <ul className="category-tiles">
          {categoryTiles.map((tile) => (
            <li key={tile.label}>
              <Link
                className={`category-tile category-tile--${tile.photo}`}
                to={`/experiences?category=${encodeURIComponent(tile.label)}`}
              >
                <span className="category-tile__body">
                  <strong>{tile.label}</strong>
                  <small>{tile.hint}</small>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>

      <section className="container home-section" data-reveal aria-labelledby="featured-title" aria-busy={loading}>
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
            onRetry={() => void featuredQuery.refetch()}
          />
        ) : featured.length === 0 ? (
          <EmptyState
            title="Todavía no hay experiencias publicadas"
            description="Pronto encontrarás nuevas experiencias aquí."
          />
        ) : (
          <div className="experience-grid">
            {featured.map((experience) => (
              <Card experience={experience} key={experience.id} />
            ))}
          </div>
        )}
      </section>

      <section className="container home-section" data-reveal aria-labelledby="trust-title">
        <h2 className="home-section__title" id="trust-title">Por qué reservar aquí</h2>
        <ul className="trust-grid">
          {trustPoints.map((point) => {
            const Icon = point.icon;
            return (
              <li className="surface-panel trust-card" key={point.title}>
                <span className="trust-card__icon" aria-hidden="true"><Icon size={20} /></span>
                <h3>{point.title}</h3>
                <p>{point.text}</p>
              </li>
            );
          })}
        </ul>
      </section>

      <section className="container home-section" data-reveal aria-labelledby="map-band-title">
        <div className="home-band">
          <div className="home-band__content">
            <span className="home-band__eyebrow">Mapa</span>
            <h2 id="map-band-title">Mira dónde ocurre cada experiencia</h2>
            <p>
              Del Malecón a la Cordillera Central: ubica las actividades sobre el mapa,
              filtra por categoría y precio, o busca las que están más cerca de ti.
            </p>
            <Link className="button-link button-link--gold" to="/experiences/map">
              <MapPinned size={18} aria-hidden="true" /> Abrir el mapa
            </Link>
          </div>
        </div>
      </section>

      <section className="container home-section" id="como-funciona" data-reveal aria-labelledby="how-title">
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
            <p>Consulta las fechas y los cupos disponibles.</p>
          </li>
          <li className="surface-panel how-step">
            <span className="how-step__icon" aria-hidden="true"><TicketCheck size={22} /></span>
            <h3>3. Reserva</h3>
            <p>Crea tu reserva con tu cuenta y sigue su estado desde “Mis reservas”.</p>
          </li>
        </ol>
      </section>

      <section className="container home-section" data-reveal aria-labelledby="host-cta-title">
        <div className="surface-panel home-cta">
          <div className="home-cta__text">
            <h2 id="host-cta-title">¿Tienes algo que enseñar de tu zona?</h2>
            <p>
              Publica tu experiencia, define tus horarios y recibe reservas con el pago ya resuelto.
              Revisamos cada perfil antes de publicarlo.
            </p>
          </div>
          <div className="home-cta__actions">
            <Link className="button-link button-link--primary" to="/host-profile">Quiero ser anfitrión</Link>
            <Link className="button-link button-link--outline" to="/experiences">Explorar experiencias</Link>
          </div>
        </div>
      </section>
    </div>
  );
};

export default Home;
