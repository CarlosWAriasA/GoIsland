import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import axios from 'axios';
import { Filter, Search, X } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import Button from '../components/Button';
import Card from '../components/Card';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import PriceField from '../components/PriceField';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import { toApiError } from '../services/apiError';
import { experienceService } from '../services/experienceService';
import type { Experience, ExperienceSearchParams } from '../types';

interface SearchForm {
  name: string;
  location: string;
  category: string;
  minPrice: string;
  maxPrice: string;
}

const emptySearch: SearchForm = {
  name: '',
  location: '',
  category: '',
  minPrice: '',
  maxPrice: '',
};

// Último filtro usado: se recuerda para cuando el usuario vuelve al catálogo.
const STORAGE_KEY = 'goisland:catalog-filters';

const toNumber = (value: string) => (value.trim() ? Number(value) : undefined);

const toSearchQuery = (form: SearchForm): ExperienceSearchParams => ({
  location: form.location.trim() || undefined,
  category: form.category.trim() || undefined,
  minPrice: toNumber(form.minPrice),
  maxPrice: toNumber(form.maxPrice),
});

const toUrlSearchParams = (form: SearchForm) => {
  const params = new URLSearchParams();
  const query = toSearchQuery(form);
  if (form.name.trim()) params.set('q', form.name.trim());
  if (query.location) params.set('location', query.location);
  if (query.category) params.set('category', query.category);
  if (query.minPrice !== undefined) params.set('minPrice', String(query.minPrice));
  if (query.maxPrice !== undefined) params.set('maxPrice', String(query.maxPrice));
  return params;
};

const readPrice = (params: URLSearchParams, key: string) => {
  const raw = params.get(key)?.trim() || '';
  const parsed = raw ? Number(raw) : undefined;
  return parsed !== undefined && Number.isFinite(parsed) && parsed >= 0 ? raw : '';
};

const fromUrlSearchParams = (params: URLSearchParams): SearchForm => ({
  name: params.get('q')?.slice(0, 160) || '',
  location: params.get('location')?.slice(0, 160) || '',
  category: params.get('category')?.slice(0, 80) || '',
  minPrice: readPrice(params, 'minPrice'),
  maxPrice: readPrice(params, 'maxPrice'),
});

const readStoredForm = (): SearchForm | null => {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    const form = fromUrlSearchParams(new URLSearchParams(stored));
    return Object.values(form).some(Boolean) ? form : null;
  } catch {
    return null;
  }
};

const getPriceError = (form: SearchForm) => {
  const min = form.minPrice.trim() ? Number(form.minPrice) : undefined;
  const max = form.maxPrice.trim() ? Number(form.maxPrice) : undefined;
  if (min !== undefined && (!Number.isFinite(min) || min < 0)) {
    return 'El precio mínimo debe ser mayor o igual a cero.';
  }
  if (max !== undefined && (!Number.isFinite(max) || max < 0)) {
    return 'El precio máximo debe ser mayor o igual a cero.';
  }
  if (min !== undefined && max !== undefined && min > max) {
    return 'El precio mínimo no puede superar al máximo.';
  }
  return undefined;
};

const formatPrice = (value: string) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
}).format(Number(value));

const getPriceChipLabel = (form: SearchForm) => {
  const min = form.minPrice.trim();
  const max = form.maxPrice.trim();
  if (min && max) return `Precio: ${formatPrice(min)} – ${formatPrice(max)}`;
  if (min) return `Precio: desde ${formatPrice(min)}`;
  return `Precio: hasta ${formatPrice(max)}`;
};

const hasFilters = (params: ExperienceSearchParams) => Object.values(params).some(
  (value) => value !== undefined,
);

type ChipKey = 'name' | 'location' | 'category' | 'price';

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
  // Categorías reales vistas en los datos del catálogo (unión acumulada,
  // nunca inventadas): alimentan el menú desplegable de categoría.
  const [knownCategories, setKnownCategories] = useState<string[]>([]);
  const [draft, setDraft] = useState<{ source: string; form: SearchForm }>(() => {
    // Sin filtros en la URL: se recupera el último filtro usado, si existe.
    const restored = searchParams.toString() ? null : readStoredForm();
    return {
      source: searchParams.toString(),
      form: restored ?? fromUrlSearchParams(searchParams),
    };
  });
  const [error, setError] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);
  const [completedRequestKey, setCompletedRequestKey] = useState<string | null>(null);
  const form = draft.source === queryString ? draft.form : urlForm;
  const requestKey = `${queryString}::${retryCount}`;
  const loading = completedRequestKey !== requestKey;
  const priceError = getPriceError(form);
  // El backend no expone búsqueda por título: se filtra en el cliente sobre los
  // datos ya devueltos por el mismo endpoint, sin cambiar la petición.
  const nameFilter = urlForm.name.trim().toLowerCase();
  const visibleExperiences = useMemo(
    () => (nameFilter
      ? experiences.filter((experience) => experience.title.toLowerCase().includes(nameFilter))
      : experiences),
    [experiences, nameFilter],
  );

  const chips = useMemo(() => {
    const list: { key: ChipKey; label: string }[] = [];
    if (form.name.trim()) list.push({ key: 'name', label: `Nombre: ${form.name.trim()}` });
    if (form.location.trim()) list.push({ key: 'location', label: `Ubicación: ${form.location.trim()}` });
    if (form.category.trim()) list.push({ key: 'category', label: `Categoría: ${form.category.trim()}` });
    if (form.minPrice.trim() || form.maxPrice.trim()) {
      list.push({ key: 'price', label: getPriceChipLabel(form) });
    }
    return list;
  }, [form]);

  useEffect(() => {
    if (priceError) return;

    const nextParams = toUrlSearchParams(form);
    if (nextParams.toString() === queryString) return;

    const debounceTimer = window.setTimeout(() => {
      setSearchParams(nextParams, { replace: true });
    }, 450);

    return () => window.clearTimeout(debounceTimer);
  }, [form, priceError, queryString, setSearchParams]);

  // Guarda el último filtro aplicado para la próxima visita al catálogo.
  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, queryString);
    } catch {
      // Si el almacenamiento no está disponible, el catálogo sigue funcionando.
    }
  }, [queryString]);

  useEffect(() => {
    const controller = new AbortController();
    const request = hasFilters(query)
      ? experienceService.searchExperiences(query, controller.signal)
      : experienceService.getExperiences(controller.signal);

    request
      .then((data) => {
        setExperiences(data);
        setKnownCategories((current) => Array.from(new Set([
          ...current,
          ...data.map((experience) => experience.category).filter(Boolean),
        ])).sort((a, b) => a.localeCompare(b, 'es')));
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

  const applyForm = (nextForm: SearchForm) => {
    setDraft({ source: queryString, form: nextForm });
  };

  const updateForm = (field: keyof SearchForm, value: string) => {
    applyForm({ ...form, [field]: value });
  };

  const removeChip = (key: ChipKey) => {
    const next = key === 'price'
      ? { ...form, minPrice: '', maxPrice: '' }
      : { ...form, [key]: '' };
    applyForm(next);
    setSearchParams(toUrlSearchParams(next), { replace: true });
  };

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (priceError) return;
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
          <span className="experiences-hero__eyebrow">Catálogo GoIsland</span>
          <h1 id="experiences-title">Experiencias locales con disponibilidad real</h1>
          <p>Consulta actividades aprobadas, precios y cupos directamente desde GoIsland.</p>
        </div>
      </section>

      <div className="experiences-layout">
        {/* Buscar y filtrar son tareas distintas: el buscador vive fuera del módulo de filtros. */}
        <form className="experience-searchbar" onSubmit={submitSearch} role="search">
          <Input
            label="Buscar experiencias"
            placeholder="Ej. buceo en Sosúa"
            maxLength={160}
            value={form.name}
            onChange={(event) => updateForm('name', event.target.value)}
            icon={<Search size={18} />}
          />
          <Button type="submit" variant="primary" isLoading={loading}>
            <Search size={18} aria-hidden="true" />
            Buscar
          </Button>
        </form>

        <form className="experience-search surface-panel" onSubmit={submitSearch} noValidate>
          <div className="experience-search__heading">
            <Filter size={18} aria-hidden="true" />
            <h2>Filtrar experiencias</h2>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={clearSearch}
              disabled={loading || chips.length === 0}
            >
              Limpiar filtros
            </Button>
          </div>

          <div className="experience-search__fields">
            <Input
              label="Ubicación"
              placeholder="Ej. Samaná"
              maxLength={160}
              value={form.location}
              onChange={(event) => updateForm('location', event.target.value)}
            />
            <SelectField
              label="Categoría"
              value={form.category}
              onChange={(event) => updateForm('category', event.target.value)}
            >
              <option value="">Todas las categorías</option>
              {form.category && !knownCategories.includes(form.category) && (
                <option value={form.category}>{form.category}</option>
              )}
              {knownCategories.map((category) => (
                <option key={category} value={category}>{category}</option>
              ))}
            </SelectField>
            <fieldset className="experience-search__price">
              <legend>Precio (USD)</legend>
              <div className="experience-search__price-range">
                <PriceField
                  aria-label="Precio mínimo en dólares"
                  placeholder="Mín."
                  value={form.minPrice}
                  onChange={(event) => updateForm('minPrice', event.target.value)}
                  aria-invalid={priceError ? true : undefined}
                />
                <span className="experience-search__price-separator" aria-hidden="true">–</span>
                <PriceField
                  aria-label="Precio máximo en dólares"
                  placeholder="Máx."
                  value={form.maxPrice}
                  onChange={(event) => updateForm('maxPrice', event.target.value)}
                  aria-invalid={priceError ? true : undefined}
                />
              </div>
            </fieldset>
          </div>

          {priceError && <p className="field-error experience-search__error" role="alert">{priceError}</p>}
        </form>

        {chips.length > 0 && (
          <div className="filter-chips">
            <span className="filter-chips__label">Filtros activos:</span>
            <ul className="filter-chips__list">
              {chips.map((chip) => (
                <li className="filter-chip" key={chip.key}>
                  <span>{chip.label}</span>
                  <button
                    type="button"
                    className="filter-chip__remove"
                    onClick={() => removeChip(chip.key)}
                    aria-label={`Quitar filtro ${chip.label}`}
                  >
                    <X size={14} aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          </div>
        )}

        <section className="experience-results" aria-busy={loading}>
          <p className="visually-hidden" role="status" aria-live="polite">
            {loading
              ? 'Cargando experiencias.'
              : error
                ? 'No fue posible cargar las experiencias.'
                : `${visibleExperiences.length} ${visibleExperiences.length === 1 ? 'resultado disponible' : 'resultados disponibles'}.`}
          </p>
          <div className="experience-results__heading">
            <h2>Experiencias disponibles</h2>
            {!loading && !error && (
              <span className="experience-results__count-label" key={visibleExperiences.length}>
                {visibleExperiences.length} {visibleExperiences.length === 1 ? 'resultado' : 'resultados'}
              </span>
            )}
          </div>

          {loading ? (
            <SkeletonLoader />
          ) : error ? (
            <ErrorState title="No pudimos cargar el catálogo" description={error} onRetry={retry} />
          ) : visibleExperiences.length === 0 ? (
            <EmptyState
              title="Sin resultados"
              description="No hay experiencias aprobadas que coincidan con estos filtros."
              action={<Button variant="outline" onClick={clearSearch}>Ver todo el catálogo</Button>}
            />
          ) : (
            <div className="experience-grid">
              {visibleExperiences.map((experience) => (
                <Card experience={experience} key={experience.id} />
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
};

export default Experiences;
