import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { Filter, Search, TriangleAlert } from 'lucide-react';
import Button from '../components/Button';
import Card from '../components/Card';
import Input from '../components/Input';
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

const toSearchParams = (form: SearchForm): ExperienceSearchParams => ({
  location: form.location.trim() || undefined,
  category: form.category.trim() || undefined,
  maxPrice: form.maxPrice ? Number(form.maxPrice) : undefined,
});

const hasFilters = (params: ExperienceSearchParams) => Object.values(params).some(
  (value) => value !== undefined,
);

const SkeletonLoader = () => (
  <div className="experience-grid" aria-label="Cargando experiencias" aria-busy="true">
    {[1, 2, 3, 4].map((item) => (
      <div className="glass-card experience-skeleton" key={item} aria-hidden="true">
        <div className="skeleton-pulse experience-skeleton__image" />
        <div className="experience-skeleton__body">
          <div className="skeleton-pulse experience-skeleton__line experience-skeleton__line--short" />
          <div className="skeleton-pulse experience-skeleton__line" />
          <div className="skeleton-pulse experience-skeleton__line" />
        </div>
      </div>
    ))}
  </div>
);

export const Experiences = () => {
  const [experiences, setExperiences] = useState<Experience[]>([]);
  const [form, setForm] = useState<SearchForm>(emptySearch);
  const [query, setQuery] = useState<ExperienceSearchParams>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [query]);

  const updateForm = (field: keyof SearchForm, value: string) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setQuery(toSearchParams(form));
  };

  const clearSearch = () => {
    setForm(emptySearch);
    setLoading(true);
    setQuery({});
  };

  const retry = () => {
    setLoading(true);
    setQuery((current) => ({ ...current }));
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

      <form className="experience-search glass-panel" onSubmit={submitSearch}>
        <div className="experience-search__heading">
          <Filter size={20} aria-hidden="true" />
          <h2>Buscar experiencias</h2>
        </div>
        <div className="experience-search__fields">
          <Input
            label="Ubicación"
            placeholder="Ej. Samaná"
            value={form.location}
            onChange={(event) => updateForm('location', event.target.value)}
          />
          <Input
            label="Categoría"
            placeholder="Ej. Naturaleza"
            value={form.category}
            onChange={(event) => updateForm('category', event.target.value)}
          />
          <Input
            label="Precio máximo (USD)"
            type="number"
            min="0"
            step="0.01"
            placeholder="Sin límite"
            value={form.maxPrice}
            onChange={(event) => updateForm('maxPrice', event.target.value)}
          />
        </div>
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

      <section className="experience-results" aria-live="polite" aria-busy={loading}>
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
          <div className="result-state glass-panel" role="alert">
            <TriangleAlert size={40} aria-hidden="true" />
            <h3>No pudimos cargar el catálogo</h3>
            <p>{error}</p>
            <Button variant="outline" onClick={retry}>Reintentar</Button>
          </div>
        ) : experiences.length === 0 ? (
          <div className="result-state glass-panel">
            <Search size={40} aria-hidden="true" />
            <h3>Sin resultados</h3>
            <p>No hay experiencias aprobadas que coincidan con estos filtros.</p>
            <Button variant="outline" onClick={clearSearch}>Ver todo el catálogo</Button>
          </div>
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
