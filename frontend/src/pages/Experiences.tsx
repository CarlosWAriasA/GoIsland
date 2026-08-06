import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
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
import { experienceKeys, queryRefresh } from '../queries/queryKeys';
import type { Experience, ExperienceSearchParams, PagedResponse } from '../types';

interface SearchForm {
  name: string;
  location: string;
  category: string;
  minPrice: string;
  maxPrice: string;
  language: string;
  difficulty: string;
  accessible: string;
  sort: ExperienceSearchParams['sort'];
}

const emptySearch: SearchForm = {
  name: '',
  location: '',
  category: '',
  minPrice: '',
  maxPrice: '',
  language: '',
  difficulty: '',
  accessible: '',
  sort: 'relevance',
};

const STORAGE_KEY = 'goisland:catalog-filters';

const toNumber = (value: string) => (value.trim() ? Number(value) : undefined);

const toSearchQuery = (form: SearchForm): ExperienceSearchParams => ({
  query: form.name.trim() || undefined,
  location: form.location.trim() || undefined,
  category: form.category.trim() || undefined,
  minPrice: toNumber(form.minPrice),
  maxPrice: toNumber(form.maxPrice),
  language: form.language.trim() || undefined,
  difficulty: form.difficulty || undefined,
  accessible: form.accessible === 'true' ? true : undefined,
  sort: form.sort || undefined,
});

const toUrlSearchParams = (form: SearchForm, page = 1) => {
  const params = new URLSearchParams();
  const query = toSearchQuery(form);
  if (form.name.trim()) params.set('q', form.name.trim());
  if (query.location) params.set('location', query.location);
  if (query.category) params.set('category', query.category);
  if (query.minPrice !== undefined) params.set('minPrice', String(query.minPrice));
  if (query.maxPrice !== undefined) params.set('maxPrice', String(query.maxPrice));
  if (query.language) params.set('language', query.language);
  if (query.difficulty) params.set('difficulty', query.difficulty);
  if (query.accessible) params.set('accessible', 'true');
  if (query.sort && query.sort !== 'relevance') params.set('sort', query.sort);
  if (page > 1) params.set('page', String(page));
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
  language: params.get('language')?.slice(0, 80) || '',
  difficulty: ['Easy', 'Moderate', 'Demanding'].includes(params.get('difficulty') || '')
    ? params.get('difficulty')!
    : '',
  accessible: params.get('accessible') === 'true' ? 'true' : '',
  sort: ['relevance', 'newest', 'priceAsc', 'priceDesc', 'rating'].includes(params.get('sort') || '')
    ? params.get('sort') as ExperienceSearchParams['sort']
    : 'relevance',
});

const readPage = (params: URLSearchParams) => {
  const page = Number(params.get('page'));
  return Number.isInteger(page) && page > 0 ? page : 1;
};

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

type ChipKey = 'name' | 'location' | 'category' | 'price' | 'language' | 'difficulty' | 'accessible';

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
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const queryString = searchParams.toString();
  const urlForm = useMemo(
    () => fromUrlSearchParams(new URLSearchParams(queryString)),
    [queryString],
  );
  const searchFilters = useMemo(
    () => ({ ...toSearchQuery(urlForm), page: readPage(new URLSearchParams(queryString)), pageSize: 24 }),
    [urlForm, queryString],
  );
  const [draft, setDraft] = useState<{ source: string; form: SearchForm }>(() => {
    const restored = searchParams.toString() ? null : readStoredForm();
    return {
      source: searchParams.toString(),
      form: restored ?? fromUrlSearchParams(searchParams),
    };
  });
  const catalogQuery = useQuery({
    queryKey: experienceKeys.search(searchFilters),
    queryFn: ({ signal }) => experienceService.searchExperiences(searchFilters, signal),
    placeholderData: (previousData) => previousData,
    refetchInterval: queryRefresh.catalog,
    refetchOnMount: 'always',
  });
  const experiences = catalogQuery.data?.items ?? [];
  const totalItems = catalogQuery.data?.totalItems ?? 0;
  const totalPages = catalogQuery.data?.totalPages ?? 0;
  const error = !catalogQuery.data && catalogQuery.error
    ? toApiError(catalogQuery.error, 'No fue posible cargar las experiencias.').message
    : null;
  const form = draft.source === queryString ? draft.form : urlForm;
  const loading = catalogQuery.isPending;
  const priceError = getPriceError(form);
  const currentPage = searchFilters.page ?? 1;
  const knownCategories = Array.from(new Set(
    queryClient
      .getQueriesData<PagedResponse<Experience>>({ queryKey: experienceKeys.searches() })
      .flatMap(([, data]) => data?.items.map((experience) => experience.category) ?? [])
      .filter(Boolean),
  )).sort((a, b) => a.localeCompare(b, 'es'));

  const chips = useMemo(() => {
    const list: { key: ChipKey; label: string }[] = [];
    if (form.name.trim()) list.push({ key: 'name', label: `Nombre: ${form.name.trim()}` });
    if (form.location.trim()) list.push({ key: 'location', label: `Ubicación: ${form.location.trim()}` });
    if (form.category.trim()) list.push({ key: 'category', label: `Categoría: ${form.category.trim()}` });
    if (form.minPrice.trim() || form.maxPrice.trim()) {
      list.push({ key: 'price', label: getPriceChipLabel(form) });
    }
    if (form.language.trim()) list.push({ key: 'language', label: `Idioma: ${form.language.trim()}` });
    if (form.difficulty) {
      const labels = { Easy: 'Fácil', Moderate: 'Moderada', Demanding: 'Exigente' };
      list.push({ key: 'difficulty', label: `Dificultad: ${labels[form.difficulty as keyof typeof labels]}` });
    }
    if (form.accessible) list.push({ key: 'accessible', label: 'Con información de accesibilidad' });
    return list;
  }, [form]);

  useEffect(() => {
    if (priceError) return;
    if (draft.source !== queryString) return;

    const nextParams = toUrlSearchParams(form, 1);
    if (nextParams.toString() === queryString) return;

    const debounceTimer = window.setTimeout(() => {
      setSearchParams(nextParams, { replace: true });
    }, 450);

    return () => window.clearTimeout(debounceTimer);
  }, [draft.source, form, priceError, queryString, setSearchParams]);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, queryString);
    } catch {
      void 0;
    }
  }, [queryString]);

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
    setSearchParams(toUrlSearchParams(next, 1), { replace: true });
  };

  const submitSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (priceError) return;
    setSearchParams(toUrlSearchParams(form, 1));
  };

  const clearSearch = () => {
    setDraft({ source: queryString, form: emptySearch });
    setSearchParams(new URLSearchParams(), { replace: true });
  };

  const goToPage = (page: number) => {
    setSearchParams(toUrlSearchParams(urlForm, page));
    document.getElementById('experience-results')?.scrollIntoView({ behavior: 'smooth' });
  };

  return (
    <div className="experiences-page animate-fade-in">
      <section className="experiences-hero" aria-labelledby="experiences-title">
        <div className="experiences-hero__content">
          <span className="experiences-hero__eyebrow">Catálogo GoIsland</span>
          <h1 id="experiences-title">Encuentra tu próxima experiencia</h1>
          <p>Encuentra actividades por lugar, precio y disponibilidad.</p>
        </div>
      </section>

      <div className="experiences-layout">
        <form className="experience-searchbar" onSubmit={submitSearch} role="search">
          <Input
            label="Buscar experiencias"
            placeholder="Ej. buceo en Sosúa"
            maxLength={160}
            value={form.name}
            onChange={(event) => updateForm('name', event.target.value)}
            icon={<Search size={18} />}
          />
          <Button type="submit" variant="primary" isLoading={catalogQuery.isFetching}>
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
            <Input
              label="Idioma"
              placeholder="Ej. Español"
              maxLength={80}
              value={form.language}
              onChange={(event) => updateForm('language', event.target.value)}
            />
            <SelectField
              label="Dificultad"
              value={form.difficulty}
              onChange={(event) => updateForm('difficulty', event.target.value)}
            >
              <option value="">Cualquier dificultad</option>
              <option value="Easy">Fácil</option>
              <option value="Moderate">Moderada</option>
              <option value="Demanding">Exigente</option>
            </SelectField>
            <SelectField
              label="Accesibilidad"
              value={form.accessible}
              onChange={(event) => updateForm('accessible', event.target.value)}
            >
              <option value="">Todas las experiencias</option>
              <option value="true">Con información de accesibilidad</option>
            </SelectField>
            <SelectField
              label="Ordenar por"
              value={form.sort}
              onChange={(event) => updateForm('sort', event.target.value)}
            >
              <option value="relevance">Relevancia</option>
              <option value="newest">Más recientes</option>
              <option value="priceAsc">Menor precio</option>
              <option value="priceDesc">Mayor precio</option>
              <option value="rating">Mejor valoración</option>
            </SelectField>
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

        <section className="experience-results" id="experience-results" aria-busy={catalogQuery.isFetching}>
          <p className="visually-hidden" role="status" aria-live="polite">
            {loading
              ? 'Cargando experiencias.'
              : error
                ? 'No fue posible cargar las experiencias.'
                : `${totalItems} ${totalItems === 1 ? 'resultado disponible' : 'resultados disponibles'}.`}
          </p>
          <div className="experience-results__heading">
            <h2>Experiencias disponibles</h2>
            {!loading && !error && (
              <span className="experience-results__count-label" key={totalItems}>
                {totalItems} {totalItems === 1 ? 'resultado' : 'resultados'}
              </span>
            )}
          </div>

          {loading ? (
            <SkeletonLoader />
          ) : error ? (
            <ErrorState
              title="No pudimos cargar el catálogo"
              description={error}
              onRetry={() => void catalogQuery.refetch()}
            />
          ) : experiences.length === 0 ? (
            <EmptyState
              title="Sin resultados"
              description="No encontramos experiencias con estos filtros."
              action={<Button variant="outline" onClick={clearSearch}>Ver todo el catálogo</Button>}
            />
          ) : (
            <div className="experience-grid">
              {experiences.map((experience) => (
                <Card experience={experience} key={experience.id} />
              ))}
            </div>
          )}
          {!loading && !error && totalPages > 1 && (
            <nav className="catalog-pagination" aria-label="Páginas del catálogo">
              <Button
                variant="outline"
                disabled={currentPage <= 1}
                onClick={() => goToPage(currentPage - 1)}
              >
                Anterior
              </Button>
              <span>Página {currentPage} de {totalPages}</span>
              <Button
                variant="outline"
                disabled={currentPage >= totalPages}
                onClick={() => goToPage(currentPage + 1)}
              >
                Siguiente
              </Button>
            </nav>
          )}
        </section>
      </div>
    </div>
  );
};

export default Experiences;
