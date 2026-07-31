import {
  CalendarDays,
  ImagePlus,
  Infinity as InfinityIcon,
  LocateFixed,
  MapPin,
  Pencil,
  Plus,
  Send,
  Trash2,
  UsersRound,
  X,
} from 'lucide-react';
import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import PriceField from '../components/PriceField';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import TextAreaField from '../components/TextAreaField';
import { EXPERIENCE_CATEGORIES } from '../constants/experienceCategories';
import { getFieldError, toApiError } from '../services/apiError';
import type { ApiError } from '../services/apiError';
import { resolveApiAssetUrl } from '../services/api';
import { hostExperienceService } from '../services/hostExperienceService';
import { reverseGeocodeLocation } from '../services/googleMapsService';
import type {
  ExperienceImage,
  ManagedExperience,
  ManagedExperienceRequest,
} from '../types';
import { getModerationLabel, getModerationTone } from '../utils/moderationStatus';

const ExperienceMap = lazy(() => import('../components/ExperienceMap'));
const MAX_IMAGES = 10;
const MAX_IMAGE_SIZE = 5 * 1024 * 1024;
const ACCEPTED_IMAGE_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp']);
const parseList = (value: string) => value.split(',').map((item) => item.trimStart());

const createEmptyForm = (): ManagedExperienceRequest => ({
  title: '',
  shortDescription: '',
  description: '',
  durationMinutes: null,
  timeZoneId: 'America/Santo_Domingo',
  meetingPointInstructions: '',
  pickupInformation: null,
  whatIsIncluded: [],
  whatIsNotIncluded: [],
  whatToBring: [],
  guestRequirements: '',
  minimumAge: null,
  difficulty: '',
  accessibilityInformation: '',
  languages: [],
  cancellationPolicy: '',
  tags: [],
  itinerary: [],
  location: '',
  latitude: null,
  longitude: null,
  category: '',
  price: 0,
  capacity: 1,
  isUnlimitedCapacity: false,
});

const formatCurrency = (amount: number) => amount === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(amount);

interface LocationPickerProps {
  location: string;
  latitude: number | null;
  longitude: number | null;
  onChange: (latitude: number | null, longitude: number | null, location?: string) => void;
  error?: string;
}

const LocationPicker = ({ location, latitude, longitude, onChange, error }: LocationPickerProps) => {
  const [locating, setLocating] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const selectedPoint = latitude !== null && longitude !== null ? { latitude, longitude } : null;

  const useCurrentLocation = () => {
    if (!navigator.geolocation) {
      setMessage('Este dispositivo no permite conocer tu ubicación.');
      return;
    }
    setLocating(true);
    setMessage(null);
    navigator.geolocation.getCurrentPosition(
      async (position) => {
        const selectedLatitude = Number(position.coords.latitude.toFixed(6));
        const selectedLongitude = Number(position.coords.longitude.toFixed(6));
        try {
          const address = await reverseGeocodeLocation(selectedLatitude, selectedLongitude);
          onChange(selectedLatitude, selectedLongitude, address);
          setMessage(null);
        } catch {
          onChange(selectedLatitude, selectedLongitude);
          setMessage('No pudimos obtener la dirección. Puedes escribirla manualmente.');
        } finally {
          setLocating(false);
        }
      },
      () => {
        setMessage('No pudimos usar tu ubicación. Puedes señalar el lugar directamente en el mapa.');
        setLocating(false);
      },
      { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 },
    );
  };

  return (
    <div className="location-picker">
      <div className="location-picker__heading">
        <strong>Dirección</strong>
        <Button type="button" variant="outline" size="sm" onClick={useCurrentLocation} isLoading={locating}>
          <LocateFixed size={17} aria-hidden="true" /> Usar mi ubicación
        </Button>
      </div>
      {message && <Alert tone="info">{message}</Alert>}
      {error && <span className="field-error" role="alert">{error}</span>}
      <Suspense fallback={<Skeleton className="location-picker__map-loading" />}>
        <ExperienceMap
          selectedPoint={selectedPoint}
          onSelect={(point) => onChange(point.latitude, point.longitude, point.location)}
          searchEnabled
          searchValue={location}
          label="Selecciona la ubicación de la experiencia"
        />
      </Suspense>
      <div className="location-picker__status">
        <span>{selectedPoint ? 'Ubicación seleccionada' : 'Selecciona un lugar o marca el mapa'}</span>
        {selectedPoint && (
          <Button type="button" variant="ghost" size="sm" onClick={() => onChange(null, null)}>
            Quitar punto
          </Button>
        )}
      </div>
    </div>
  );
};

interface PendingImage {
  file: File;
  previewUrl: string;
  altText: string;
  isCover: boolean;
}

interface ImagePickerProps {
  existing: ExperienceImage[];
  pending: PendingImage[];
  onExistingRemove: (image: ExperienceImage) => void;
  onExistingChange: (images: ExperienceImage[]) => void;
  onPendingChange: (images: PendingImage[]) => void;
  error: string | null;
  onError: (message: string | null) => void;
}

const ImagePicker = ({
  existing,
  pending,
  onExistingRemove,
  onExistingChange,
  onPendingChange,
  error,
  onError,
}: ImagePickerProps) => {
  const total = existing.length + pending.length;

  const addImages = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (selected.length === 0) return;
    if (total + selected.length > MAX_IMAGES) {
      onError(`Puedes agregar hasta ${MAX_IMAGES} imágenes en total.`);
      return;
    }
    const invalidType = selected.find((file) => !ACCEPTED_IMAGE_TYPES.has(file.type));
    if (invalidType) {
      onError('Usa imágenes JPG, PNG o WebP.');
      return;
    }
    const tooLarge = selected.find((file) => file.size > MAX_IMAGE_SIZE);
    if (tooLarge) {
      onError(`Cada imagen puede pesar hasta 5 MB. “${tooLarge.name}” supera el límite.`);
      return;
    }
    onError(null);
    const alreadyHasCover = existing.some((image) => image.isCover)
      || pending.some((image) => image.isCover);
    onPendingChange([
      ...pending,
      ...selected.map((file, index) => ({
        file,
        previewUrl: URL.createObjectURL(file),
        altText: '',
        isCover: !alreadyHasCover && index === 0,
      })),
    ]);
  };

  const removePending = (index: number) => {
    URL.revokeObjectURL(pending[index].previewUrl);
    const wasCover = pending[index].isCover;
    const remaining = pending.filter((_, currentIndex) => currentIndex !== index);
    if (wasCover && !existing.some((image) => image.isCover) && remaining.length > 0) {
      remaining[0] = { ...remaining[0], isCover: true };
    }
    onPendingChange(remaining);
  };

  const selectExistingCover = (imageId: number) => {
    onExistingChange(existing.map((image) => ({ ...image, isCover: image.id === imageId })));
    onPendingChange(pending.map((image) => ({ ...image, isCover: false })));
  };

  const selectPendingCover = (pendingIndex: number) => {
    onExistingChange(existing.map((image) => ({ ...image, isCover: false })));
    onPendingChange(pending.map((image, index) => ({ ...image, isCover: index === pendingIndex })));
  };

  return (
    <section className="experience-images" aria-labelledby="experience-images-title">
      <div className="experience-images__heading">
        <strong id="experience-images-title">Fotos</strong>
      </div>
      {error && <Alert tone="error">{error}</Alert>}
      <div className="experience-images__grid">
        {existing.map((image, index) => (
          <div className="experience-image-editor" key={image.id}>
            <figure className="experience-image-preview">
              <img src={resolveApiAssetUrl(image.thumbnailUrl)} alt={image.altText || `Imagen ${index + 1}`} />
              {image.isCover && <figcaption>Portada</figcaption>}
              <button type="button" onClick={() => onExistingRemove(image)} aria-label={`Quitar imagen ${index + 1}`}>
                <X size={16} aria-hidden="true" />
              </button>
            </figure>
            <input
              aria-label={`Descripción de la imagen ${index + 1}`}
              maxLength={180}
              placeholder="Describe la foto"
              value={image.altText}
              onChange={(event) => onExistingChange(existing.map((candidate) => (
                candidate.id === image.id ? { ...candidate, altText: event.target.value } : candidate
              )))}
            />
            <label className="experience-image-cover">
              <input
                type="radio"
                name="experience-cover"
                checked={image.isCover}
                onChange={() => selectExistingCover(image.id)}
              />
              Usar como portada
            </label>
          </div>
        ))}
        {pending.map((image, index) => (
          <div className="experience-image-editor" key={image.previewUrl}>
            <figure className="experience-image-preview experience-image-preview--pending">
              <img src={image.previewUrl} alt={image.altText || `Nueva imagen ${index + 1}`} />
              <figcaption>{image.isCover ? 'Portada' : 'Nueva'}</figcaption>
              <button type="button" onClick={() => removePending(index)} aria-label={`Quitar nueva imagen ${index + 1}`}>
                <X size={16} aria-hidden="true" />
              </button>
            </figure>
            <input
              aria-label={`Descripción de la nueva imagen ${index + 1}`}
              maxLength={180}
              placeholder="Describe la foto"
              value={image.altText}
              onChange={(event) => onPendingChange(pending.map((candidate, candidateIndex) => (
                candidateIndex === index ? { ...candidate, altText: event.target.value } : candidate
              )))}
            />
            <label className="experience-image-cover">
              <input
                type="radio"
                name="experience-cover"
                checked={image.isCover}
                onChange={() => selectPendingCover(index)}
              />
              Usar como portada
            </label>
          </div>
        ))}
        {total < MAX_IMAGES && (
          <label className="experience-image-add">
            <ImagePlus size={24} aria-hidden="true" />
            <strong>Agregar fotos</strong>
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              multiple
              onChange={addImages}
            />
          </label>
        )}
      </div>
    </section>
  );
};

export const HostExperiences = () => {
  const [experiences, setExperiences] = useState<ManagedExperience[]>([]);
  const [form, setForm] = useState<ManagedExperienceRequest>(createEmptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [existingImages, setExistingImages] = useState<ExperienceImage[]>([]);
  const [removedImageIds, setRemovedImageIds] = useState<number[]>([]);
  const [pendingImages, setPendingImages] = useState<PendingImage[]>([]);
  const pendingImagesRef = useRef<PendingImage[]>([]);
  const [imageError, setImageError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);

  const visibleImages = useMemo(
    () => existingImages.filter((image) => !removedImageIds.includes(image.id)),
    [existingImages, removedImageIds],
  );

  const clearPendingPreviews = () => {
    pendingImages.forEach((image) => URL.revokeObjectURL(image.previewUrl));
    setPendingImages([]);
  };

  const resetImages = (images: ExperienceImage[] = []) => {
    clearPendingPreviews();
    setExistingImages(images);
    setRemovedImageIds([]);
    setImageError(null);
  };

  const retryLoad = () => {
    setLoading(true);
    setError(null);
    setRetryCount((current) => current + 1);
  };

  useEffect(() => {
    const controller = new AbortController();
    hostExperienceService.getMine(controller.signal)
      .then((data) => {
        setExperiences(data);
        setError(null);
      })
      .catch((requestError: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(requestError).message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [retryCount]);

  useEffect(() => {
    if (!showForm) return undefined;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !submitting) setShowForm(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [showForm, submitting]);

  useEffect(() => {
    pendingImagesRef.current = pendingImages;
  }, [pendingImages]);

  useEffect(() => () => {
    pendingImagesRef.current.forEach((image) => URL.revokeObjectURL(image.previewUrl));
  }, []);

  const startCreate = () => {
    setEditingId(null);
    setForm(createEmptyForm());
    setFormError(null);
    resetImages();
    setShowForm(true);
  };

  const startEdit = (experience: ManagedExperience) => {
    setEditingId(experience.id);
    setForm({
      title: experience.title,
      shortDescription: experience.shortDescription,
      description: experience.description,
      durationMinutes: experience.durationMinutes,
      timeZoneId: experience.timeZoneId,
      meetingPointInstructions: experience.meetingPointInstructions,
      pickupInformation: experience.pickupInformation,
      whatIsIncluded: experience.whatIsIncluded,
      whatIsNotIncluded: experience.whatIsNotIncluded,
      whatToBring: experience.whatToBring,
      guestRequirements: experience.guestRequirements,
      minimumAge: experience.minimumAge,
      difficulty: experience.difficulty,
      accessibilityInformation: experience.accessibilityInformation,
      languages: experience.languages,
      cancellationPolicy: experience.cancellationPolicy,
      tags: experience.tags,
      itinerary: experience.itinerary,
      location: experience.location,
      latitude: experience.latitude,
      longitude: experience.longitude,
      category: experience.category,
      price: experience.price,
      capacity: experience.isUnlimitedCapacity ? 1 : experience.capacity,
      isUnlimitedCapacity: experience.isUnlimitedCapacity,
    });
    setFormError(null);
    resetImages(experience.images);
    setShowForm(true);
  };

  const closeForm = () => {
    if (submitting) return;
    setShowForm(false);
    setEditingId(null);
    setFormError(null);
    resetImages();
  };

  const handleLocationChange = (
    latitude: number | null,
    longitude: number | null,
    location?: string,
  ) => {
    setForm((current) => ({
      ...current,
      latitude,
      longitude,
      location: location?.trim() || current.location,
    }));
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setFormError(null);
    setImageError(null);
    setSuccess(null);
    let saved: ManagedExperience | null = null;
    let synchronizedImages: ExperienceImage[] = [];
    try {
      saved = editingId
        ? await hostExperienceService.update(editingId, form)
        : await hostExperienceService.create(form);
      setEditingId(saved.id);
      setExperiences((current) => {
        const exists = current.some((item) => item.id === saved!.id);
        return exists
          ? current.map((item) => item.id === saved!.id ? saved! : item)
          : [saved!, ...current];
      });

      synchronizedImages = saved.images;
      for (const imageId of removedImageIds) {
        synchronizedImages = await hostExperienceService.deleteImage(saved.id, imageId);
      }
      if (pendingImages.length > 0) {
        synchronizedImages = await hostExperienceService.uploadImages(
          saved.id,
          pendingImages.map((image) => ({
            file: image.file,
            altText: image.altText,
            isCover: image.isCover,
          })),
        );
      }
      const remainingExisting = visibleImages
        .filter((image) => !removedImageIds.includes(image.id))
        .sort((first, second) => Number(first.isCover) - Number(second.isCover));
      for (const image of remainingExisting) {
        synchronizedImages = await hostExperienceService.updateImage(saved.id, image.id, {
          altText: image.altText || `Foto de ${saved.title}`,
          isCover: image.isCover,
        });
      }

      const completed = { ...saved, images: synchronizedImages };
      setExperiences((current) => current.map((item) => item.id === completed.id ? completed : item));
      setSuccess(editingId
        ? 'La experiencia volvió a borrador después de guardar los cambios.'
        : 'La experiencia fue creada como borrador. Envíala cuando esté lista.');
      closeForm();
    } catch (requestError: unknown) {
      const apiError = toApiError(requestError);
      if (saved) {
        setExistingImages(synchronizedImages);
        setRemovedImageIds([]);
        setImageError(`El borrador se guardó, pero no pudimos sincronizar la galería: ${apiError.message}`);
      } else {
        setFormError(apiError);
      }
    } finally {
      setSubmitting(false);
    }
  };

  const submitForReview = async (id: number) => {
    setBusyId(id);
    setError(null);
    setSuccess(null);
    try {
      const updated = await hostExperienceService.submit(id);
      setExperiences((current) => current.map((item) => item.id === id ? updated : item));
      setSuccess('La experiencia fue enviada a revisión.');
    } catch (requestError: unknown) {
      setError(toApiError(requestError).message);
    } finally {
      setBusyId(null);
    }
  };

  const removeExperience = async (experience: ManagedExperience) => {
    const confirmed = window.confirm(`¿Eliminar el borrador “${experience.title}”?`);
    if (!confirmed) return;

    setBusyId(experience.id);
    setError(null);
    try {
      await hostExperienceService.remove(experience.id);
      setExperiences((current) => current.filter((item) => item.id !== experience.id));
      setSuccess('La experiencia fue eliminada.');
    } catch (requestError: unknown) {
      setError(toApiError(requestError).message);
    } finally {
      setBusyId(null);
    }
  };

  const drawer = showForm && createPortal(
    <div className="experience-drawer-layer" role="presentation">
      <button className="experience-drawer__backdrop" type="button" aria-label="Cerrar formulario" onClick={closeForm} />
      <aside className="experience-drawer" role="dialog" aria-modal="true" aria-labelledby="experience-form-title">
        <header className="experience-drawer__header">
          <div>
            <span>{editingId ? 'Editar borrador' : 'Nueva experiencia'}</span>
            <h2 id="experience-form-title">{editingId ? 'Editar experiencia' : 'Crear experiencia'}</h2>
          </div>
          <button type="button" onClick={closeForm} disabled={submitting} aria-label="Cerrar formulario">
            <X size={21} aria-hidden="true" />
          </button>
        </header>
        <form className="experience-drawer__form" onSubmit={handleSubmit} noValidate>
          <div className="experience-drawer__content">
            {formError && <Alert tone="error">{formError.message}</Alert>}
            <section className="experience-form-section">
              <div className="experience-form-section__heading">
                <span>1</span><h3>Información básica</h3>
              </div>
              <Input
                label="Título"
                value={form.title}
                onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
                error={formError ? getFieldError(formError, 'Title') : undefined}
                required
              />
              <TextAreaField
                label="Resumen"
                hint="Una frase clara para las tarjetas del catálogo."
                value={form.shortDescription}
                onChange={(event) => setForm((current) => ({ ...current, shortDescription: event.target.value }))}
                error={formError ? getFieldError(formError, 'ShortDescription') : undefined}
                rows={2}
                maxLength={300}
              />
              <TextAreaField
                label="Descripción"
                value={form.description}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                error={formError ? getFieldError(formError, 'Description') : undefined}
                rows={6}
                required
              />
              <SelectField
                label="Categoría"
                value={form.category}
                onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))}
                error={formError ? getFieldError(formError, 'Category') : undefined}
                required
              >
                <option value="">Selecciona una categoría</option>
                {EXPERIENCE_CATEGORIES.map((category) => (
                  <option value={category} key={category}>{category}</option>
                ))}
              </SelectField>
              <Input
                label="Duración en minutos"
                type="number"
                min="1"
                value={form.durationMinutes ?? ''}
                onChange={(event) => setForm((current) => ({
                  ...current,
                  durationMinutes: event.target.value ? Number(event.target.value) : null,
                }))}
              />
            </section>

            <section className="experience-form-section">
              <div className="experience-form-section__heading">
                <span>2</span><h3>Precio y capacidad</h3>
              </div>
              <label className="choice-card">
                <input
                  type="checkbox"
                  checked={form.price === 0}
                  onChange={(event) => setForm((current) => ({
                    ...current,
                    price: event.target.checked ? 0 : Math.max(current.price, 1),
                  }))}
                />
                <span className="choice-card__icon">$0</span>
                <span><strong>Experiencia gratis</strong></span>
              </label>
              <PriceField
                label="Precio por persona (USD)"
                value={form.price}
                onChange={(event) => setForm((current) => ({ ...current, price: Number(event.target.value) }))}
                error={formError ? getFieldError(formError, 'Price') : undefined}
                disabled={form.price === 0}
                required
              />
              <label className="choice-card">
                <input
                  type="checkbox"
                  checked={form.isUnlimitedCapacity}
                  onChange={(event) => setForm((current) => ({
                    ...current,
                    isUnlimitedCapacity: event.target.checked,
                  }))}
                />
                <span className="choice-card__icon"><InfinityIcon size={21} aria-hidden="true" /></span>
                <span><strong>Sin límite de personas</strong></span>
              </label>
              <Input
                label="Capacidad"
                type="number"
                min="1"
                step="1"
                value={form.capacity}
                onChange={(event) => setForm((current) => ({ ...current, capacity: Number(event.target.value) }))}
                error={formError ? getFieldError(formError, 'Capacity') : undefined}
                icon={<UsersRound size={18} />}
                disabled={form.isUnlimitedCapacity}
                required={!form.isUnlimitedCapacity}
              />
            </section>

            <section className="experience-form-section">
              <div className="experience-form-section__heading">
                <span>3</span><h3>Lugar</h3>
              </div>
              <LocationPicker
                location={form.location}
                latitude={form.latitude}
                longitude={form.longitude}
                onChange={handleLocationChange}
                error={formError
                  ? getFieldError(formError, 'Location')
                    ?? getFieldError(formError, 'Latitude')
                    ?? getFieldError(formError, 'Longitude')
                  : undefined}
              />
            </section>

            <section className="experience-form-section">
              <div className="experience-form-section__heading">
                <span>4</span><h3>Fotos</h3>
              </div>
              <ImagePicker
                existing={visibleImages}
                pending={pendingImages}
                onExistingRemove={(image) => setRemovedImageIds((current) => [...current, image.id])}
                onExistingChange={(images) => setExistingImages((current) => current.map((image) => (
                  images.find((candidate) => candidate.id === image.id) ?? image
                )))}
                onPendingChange={setPendingImages}
                error={imageError}
                onError={setImageError}
              />
            </section>

            <details className="experience-advanced">
              <summary>Completar información para publicar</summary>
              <div className="experience-advanced__content">
                <TextAreaField
                  label="Punto de encuentro"
                  value={form.meetingPointInstructions}
                  onChange={(event) => setForm((current) => ({ ...current, meetingPointInstructions: event.target.value }))}
                  rows={3}
                />
                <TextAreaField
                  label="Recogida (opcional)"
                  value={form.pickupInformation ?? ''}
                  onChange={(event) => setForm((current) => ({
                    ...current,
                    pickupInformation: event.target.value || null,
                  }))}
                  rows={3}
                />
                <Input
                  label="Qué incluye"
                  hint="Separa cada elemento con una coma."
                  value={form.whatIsIncluded.join(', ')}
                  onChange={(event) => setForm((current) => ({ ...current, whatIsIncluded: parseList(event.target.value) }))}
                />
                <Input
                  label="Qué no incluye"
                  hint="Separa cada elemento con una coma."
                  value={form.whatIsNotIncluded.join(', ')}
                  onChange={(event) => setForm((current) => ({ ...current, whatIsNotIncluded: parseList(event.target.value) }))}
                />
                <Input
                  label="Qué llevar"
                  hint="Separa cada elemento con una coma."
                  value={form.whatToBring.join(', ')}
                  onChange={(event) => setForm((current) => ({ ...current, whatToBring: parseList(event.target.value) }))}
                />
                <TextAreaField
                  label="Requisitos para participar"
                  value={form.guestRequirements}
                  onChange={(event) => setForm((current) => ({ ...current, guestRequirements: event.target.value }))}
                  rows={3}
                />
                <Input
                  label="Edad mínima (opcional)"
                  type="number"
                  min="0"
                  value={form.minimumAge ?? ''}
                  onChange={(event) => setForm((current) => ({
                    ...current,
                    minimumAge: event.target.value ? Number(event.target.value) : null,
                  }))}
                />
                <SelectField
                  label="Dificultad"
                  value={form.difficulty}
                  onChange={(event) => setForm((current) => ({ ...current, difficulty: event.target.value }))}
                >
                  <option value="">Selecciona una opción</option>
                  <option value="Easy">Fácil</option>
                  <option value="Moderate">Moderada</option>
                  <option value="Demanding">Exigente</option>
                </SelectField>
                <TextAreaField
                  label="Accesibilidad"
                  value={form.accessibilityInformation}
                  onChange={(event) => setForm((current) => ({
                    ...current,
                    accessibilityInformation: event.target.value,
                  }))}
                  rows={3}
                />
                <Input
                  label="Idiomas"
                  hint="Separa cada idioma con una coma."
                  value={form.languages.join(', ')}
                  onChange={(event) => setForm((current) => ({ ...current, languages: parseList(event.target.value) }))}
                />
                <SelectField
                  label="Cancelación"
                  value={form.cancellationPolicy}
                  onChange={(event) => setForm((current) => ({ ...current, cancellationPolicy: event.target.value }))}
                >
                  <option value="">Selecciona una política</option>
                  <option value="Flexible">Flexible</option>
                  <option value="Moderate">Moderada</option>
                  <option value="Strict">Estricta</option>
                </SelectField>
                <Input
                  label="Etiquetas"
                  hint="Separa cada etiqueta con una coma."
                  value={form.tags.join(', ')}
                  onChange={(event) => setForm((current) => ({ ...current, tags: parseList(event.target.value) }))}
                />

                <div className="itinerary-editor">
                  <div className="itinerary-editor__heading">
                    <strong>Itinerario</strong>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setForm((current) => ({
                        ...current,
                        itinerary: [...current.itinerary, {
                          title: '',
                          description: '',
                          durationMinutes: 30,
                          location: null,
                        }],
                      }))}
                    ><Plus size={16} aria-hidden="true" />Agregar etapa</Button>
                  </div>
                  {form.itinerary.map((item, index) => (
                    <div className="itinerary-editor__item" key={index}>
                      <strong>Etapa {index + 1}</strong>
                      <Input
                        label="Título"
                        value={item.title}
                        onChange={(event) => setForm((current) => ({
                          ...current,
                          itinerary: current.itinerary.map((candidate, candidateIndex) => (
                            candidateIndex === index ? { ...candidate, title: event.target.value } : candidate
                          )),
                        }))}
                      />
                      <TextAreaField
                        label="Descripción"
                        value={item.description}
                        onChange={(event) => setForm((current) => ({
                          ...current,
                          itinerary: current.itinerary.map((candidate, candidateIndex) => (
                            candidateIndex === index ? { ...candidate, description: event.target.value } : candidate
                          )),
                        }))}
                        rows={2}
                      />
                      <Input
                        label="Duración en minutos"
                        type="number"
                        min="1"
                        value={item.durationMinutes}
                        onChange={(event) => setForm((current) => ({
                          ...current,
                          itinerary: current.itinerary.map((candidate, candidateIndex) => (
                            candidateIndex === index
                              ? { ...candidate, durationMinutes: Number(event.target.value) }
                              : candidate
                          )),
                        }))}
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => setForm((current) => ({
                          ...current,
                          itinerary: current.itinerary.filter((_, candidateIndex) => candidateIndex !== index),
                        }))}
                      ><Trash2 size={16} aria-hidden="true" />Quitar etapa</Button>
                    </div>
                  ))}
                </div>
              </div>
            </details>
          </div>
          <footer className="experience-drawer__footer">
            <Button type="button" variant="outline" onClick={closeForm} disabled={submitting}>Cancelar</Button>
            <Button type="submit" isLoading={submitting}>Guardar borrador</Button>
          </footer>
        </form>
      </aside>
    </div>,
    document.body,
  );

  return (
    <div className="container management-page animate-fade-in">
      <header className="page-heading management-heading">
        <div>
          <span className="page-heading__eyebrow">Panel de anfitrión</span>
          <h1>Mis experiencias</h1>
          <p>Prepara borradores, envíalos a revisión y consulta la decisión real.</p>
        </div>
        <Button onClick={startCreate}><Plus size={18} aria-hidden="true" />Nueva experiencia</Button>
      </header>

      {success && <Alert tone="success">{success}</Alert>}
      {error && <Alert tone="error">{error}</Alert>}

      {loading ? (
        <div className="management-list" role="status">
          {[1, 2].map((item) => <Skeleton key={item} className="management-card management-card--loading" />)}
          <span className="visually-hidden">Cargando experiencias...</span>
        </div>
      ) : error && experiences.length === 0 ? (
        <ErrorState description={error} onRetry={retryLoad} />
      ) : experiences.length === 0 ? (
        <EmptyState
          title="Todavía no tienes experiencias"
          description="Crea un borrador con información real y envíalo a revisión cuando esté listo."
          action={<Button onClick={startCreate}>Crear experiencia</Button>}
        />
      ) : (
        <div className="management-list" aria-live="polite">
          {experiences.map((experience) => (
            <article className="management-card management-card--experience surface-panel" key={experience.id}>
              {(experience.images.find((image) => image.isCover) ?? experience.images[0]) && (
                <img
                  className="management-card__cover"
                  src={resolveApiAssetUrl(
                    (experience.images.find((image) => image.isCover) ?? experience.images[0]).cardUrl,
                  )}
                  alt={(experience.images.find((image) => image.isCover) ?? experience.images[0]).altText}
                />
              )}
              <div className="management-card__content">
                <div className="management-card__header">
                  <div>
                    <span className="management-card__reference">Experiencia #{experience.id}</span>
                    <h2>{experience.title}</h2>
                    <p><MapPin size={16} aria-hidden="true" />{experience.location}</p>
                  </div>
                  <StatusBadge tone={getModerationTone(experience.approvalStatus)}>
                    {getModerationLabel(experience.approvalStatus)}
                  </StatusBadge>
                </div>
                {experience.rejectionReason && (
                  <Alert tone="error"><strong>Motivo:</strong> {experience.rejectionReason}</Alert>
                )}
                <dl className="management-card__facts">
                  <div><dt>Categoría</dt><dd>{experience.category}</dd></div>
                  <div><dt>Precio</dt><dd>{formatCurrency(experience.price)}</dd></div>
                  <div>
                    <dt>Cupos</dt>
                    <dd>{experience.isUnlimitedCapacity ? 'Sin límite' : `${experience.availableSpots} de ${experience.capacity}`}</dd>
                  </div>
                  <div><dt>Galería</dt><dd>{experience.images.length} de {MAX_IMAGES}</dd></div>
                </dl>
                <div className="management-actions">
                  {experience.approvalStatus === 'Approved' && (
                    <Link className="button-link button-link--outline" to={`/host/experiences/${experience.id}/schedules`}>
                      <CalendarDays size={17} aria-hidden="true" />Calendario
                    </Link>
                  )}
                  {experience.approvalStatus !== 'Suspended' && (
                    <Button variant="outline" onClick={() => startEdit(experience)}>
                      <Pencil size={17} aria-hidden="true" />Editar
                    </Button>
                  )}
                  {(experience.approvalStatus === 'Draft' || experience.approvalStatus === 'Rejected') && (
                    <Button onClick={() => void submitForReview(experience.id)} isLoading={busyId === experience.id}>
                      <Send size={17} aria-hidden="true" />Enviar a revisión
                    </Button>
                  )}
                  {experience.approvalStatus === 'Draft' && (
                    <Button
                      variant="danger"
                      onClick={() => void removeExperience(experience)}
                      disabled={busyId === experience.id}
                    >
                      <Trash2 size={17} aria-hidden="true" />Eliminar
                    </Button>
                  )}
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
      {drawer}
    </div>
  );
};

export default HostExperiences;
