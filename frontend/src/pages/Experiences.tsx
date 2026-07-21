import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { Filter, Search } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import Card from '../components/Card';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import PriceField from '../components/PriceField';
import Skeleton from '../components/Skeleton';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import type { Experience, ExperienceSearchParams } from '../types';

interface SearchForm {
  location: string;
  category: string;
  maxPrice: string;
}

const emptySearch: SearchForm = {
  location: '',
  category: '',
  maxPrice: '',
};

const toSearchQuery = (form: SearchForm): ExperienceSearchParams => ({
  location: form.location.trim() || undefined,
  category: form.category.trim() || undefined,
  maxPrice: form.maxPrice ? Number(form.maxPrice) : undefined,
});

const toUrlSearchParams = (form: SearchForm) => {
  const params = new URLSearchParams();
  const query = toSearchQuery(form);
  if (query.location) params.set('location', query.location);
  if (query.category) params.set('category', query.category);
  if (query.maxPrice !== undefined) params.set('maxPrice', String(query.maxPrice));
  return params;
};

const fromUrlSearchParams = (params: URLSearchParams): SearchForm => {
  const rawMaxPrice = params.get('maxPrice')?.trim() || '';
  const parsedMaxPrice = rawMaxPrice ? Number(rawMaxPrice) : undefined;
  return {
    location: params.get('location')?.slice(0, 160) || '',
    category: params.get('category')?.slice(0, 80) || '',
    maxPrice: parsedMaxPrice !== undefined && Number.isFinite(parsedMaxPrice) && parsedMaxPrice >= 0
      ? rawMaxPrice
      : '',
  };
};

const getMaxPriceError = (value: string) => {
  if (!value) return undefined;
  const price = Number(value);
  return !Number.isFinite(price) || price < 0
    ? 'El precio maximo debe ser mayor o igual a cero.'
    : undefined;
};

const hasFilters = (params: ExperienceSearchParams) => Object.values(params).some(
  (value) => value !== undefined,
);

const SkeletonLoader = () => (
  <div className="experience-grid" aria-hidden="true">
    {[1, 2, 3, 4].map((item) => (
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

export const Experiences = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryString = searchParams.toString();
  const urlForm = useMemo(
    () => fromUrlSearchParams(new URLSearchParams(queryString)),
    [queryString],
  );
  const query = useMemo(
    () => toSearchQuery(urlForm),
    [urlForm],
  );
  const [experiences, setExperiences] = useState<Experience[]>([]);
  const [draft, setDraft] = useState<{ source: string; form: SearchForm }>(() => ({
    source: queryString,
    form: fromUrlSearchParams(searchParams),
  }));
  const [error, setError] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const [completedRequestKey, setCompletedRequestKey] = useState<string | null>(null);
  const form = draft.source === queryString ? draft.form : urlForm;
  const requestKey = `${queryString}::${retryCount}`;
  const loading = completedRequestKey !== requestKey;
  const maxPriceError = getMaxPriceError(form.maxPrice);

  useEffect(() => {
    if (maxPriceError) return;

    const nextParams = toUrlSearchParams(form);
    if (nextParams.toString() === queryString) return;

    const debounceTimer = window.setTimeout(() => {
      setSearchParams(nextParams, { replace: true });
    }, 450);

    return () => window.clearTimeout(debounceTimer);
  }, [form, maxPriceError, queryString, setSearchParams]);

  useEffect(() => {
    const controller = new AbortController();
    const request = hasFilters(query)
      ? experienceService.searchExperiences(query, controller.signal)
      : experienceService.getExperiences(controller.signal);

    request
      .then((data) => {
        setExperiences(data);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!axios.isCancel(requestError)) {
          setExperiences([]);
          setError(toApiError(requestError, 'No fue posible cargar las experiencias.').message);
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setCompletedRequestKey(requestKey);
        }
      });

    return () => controller.abort();
  }, [query, requestKey]);

  const updateForm = (field: keyof SearchForm, value: string) => {
    setDraft({
      source: queryString,
      form: { ...form, [field]: value },
    });
  };

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (maxPriceError) return;
    setSearchParams(toUrlSearchParams(form));
  };

  const clearSearch = () => {
    setDraft({ source: queryString, form: emptySearch });
    setSearchParams(new URLSearchParams(), { replace: true });
  };

  const retry = () => {
    setRetryCount((current) => current + 1);
  };

  return (
    <div className="experiences-page animate-fade-in">
      <section className="experiences-hero" aria-labelledby="experiences-title">
        <div className="experiences-hero__content">
          <span className="experiences-hero__eyebrow">Explora República Dominicana</span>
          <h1 id="experiences-title">Experiencias locales con disponibilidad real</h1>
          <p>Consulta actividades aprobadas, precios y cupos directamente desde GoIsland.</p>
        </div>
      </section>

      <form className="experience-search surface-panel" onSubmit={submitSearch} noValidate>
        <div className="experience-search__heading">
          <Filter size={20} aria-hidden="true" />
          <h2>Buscar experiencias</h2>
        </div>
        <div className="experience-search__fields">
          <Input
            label="Ubicación"
            placeholder="Ej. Samaná"
            maxLength={160}
            value={form.location}
            onChange={(event) => updateForm('location', event.target.value)}
          />
          <Input
            label="Categoría"
            placeholder="Ej. Naturaleza"
            maxLength={80}
            value={form.category}
            onChange={(event) => updateForm('category', event.target.value)}
          />
          <PriceField
            label="Precio máximo (USD)"
            placeholder="Sin límite"
            value={form.maxPrice}
            onChange={(event) => updateForm('maxPrice', event.target.value)}
            error={maxPriceError}
          />
        </div>
        <p className="experience-search__hint">Los filtros se aplican automáticamente y quedan guardados en la URL.</p>
        <div className="experience-search__actions">
          <Button type="button" variant="outline" onClick={clearSearch} disabled={loading}>
            Limpiar
          </Button>
          <Button type="submit" variant="primary" isLoading={loading}>
            <Search size={18} aria-hidden="true" />
            Buscar
          </Button>
        </div>
      </form>

      <section className="experience-results" aria-busy={loading}>
        <p className="visually-hidden" role="status" aria-live="polite">
          {loading
            ? 'Cargando experiencias.'
            : error
              ? 'No fue posible cargar las experiencias.'
              : `${experiences.length} ${experiences.length === 1 ? 'resultado disponible' : 'resultados disponibles'}.`}
        </p>
        <div className="experience-results__heading">
          <div>
            <span className="experience-results__eyebrow">Catálogo GoIsland</span>
            <h2>Experiencias disponibles</h2>
          </div>
          {!loading && !error && (
            <span>{experiences.length} {experiences.length === 1 ? 'resultado' : 'resultados'}</span>
          )}
        </div>

        {loading ? (
          <SkeletonLoader />
        ) : error ? (
          <ErrorState title="No pudimos cargar el catálogo" description={error} onRetry={retry} />
        ) : experiences.length === 0 ? (
          <EmptyState
            title="Sin resultados"
            description="No hay experiencias aprobadas que coincidan con estos filtros."
            action={<Button variant="outline" onClick={clearSearch}>Ver todo el catálogo</Button>}
          />
        ) : (
          <div className="experience-grid">
            {experiences.map((experience) => (
              <Card experience={experience} key={experience.id} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
};

export default Experiences;
