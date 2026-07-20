import { MapPin, Pencil, Plus, Send, Trash2, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import PriceField from '../components/PriceField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import TextAreaField from '../components/TextAreaField';
import { getFieldError, toApiError } from '../services/apiError';
import type { ApiError } from '../services/apiError';
import { hostExperienceService } from '../services/hostExperienceService';
import type { ManagedExperience, ManagedExperienceRequest } from '../types';
import { getModerationLabel, getModerationTone } from '../utils/moderationStatus';

const emptyForm: ManagedExperienceRequest = {
  title: '',
  description: '',
  location: '',
  category: '',
  price: 0,
  capacity: 1,
};

const formatCurrency = (amount: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
}).format(amount);

export const HostExperiences = () => {
  const [experiences, setExperiences] = useState<ManagedExperience[]>([]);
  const [form, setForm] = useState<ManagedExperienceRequest>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retryCount, setRetryCount] = useState(0);

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

  const startCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setFormError(null);
    setShowForm(true);
  };

  const startEdit = (experience: ManagedExperience) => {
    setEditingId(experience.id);
    setForm({
      title: experience.title,
      description: experience.description,
      location: experience.location,
      category: experience.category,
      price: experience.price,
      capacity: experience.capacity,
    });
    setFormError(null);
    setShowForm(true);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const closeForm = () => {
    setShowForm(false);
    setEditingId(null);
    setFormError(null);
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setFormError(null);
    setSuccess(null);
    try {
      const saved = editingId
        ? await hostExperienceService.update(editingId, form)
        : await hostExperienceService.create(form);
      setExperiences((current) => editingId
        ? current.map((item) => item.id === saved.id ? saved : item)
        : [saved, ...current]);
      setSuccess(editingId
        ? 'La experiencia volvió a borrador después de guardar los cambios.'
        : 'La experiencia fue creada como borrador. Envíala cuando esté lista.');
      closeForm();
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
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
      setSuccess('La experiencia fue enviada a moderación.');
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

      {showForm && (
        <section className="management-form surface-panel" aria-labelledby="experience-form-title">
          <div className="management-form__heading">
            <Pencil aria-hidden="true" />
            <div>
              <h2 id="experience-form-title">
                {editingId ? 'Editar experiencia' : 'Nueva experiencia'}
              </h2>
              <p>Guardar crea un borrador; la publicación requiere aprobación administrativa.</p>
            </div>
          </div>
          {formError && <Alert tone="error">{formError.message}</Alert>}
          <form onSubmit={handleSubmit} noValidate>
            <div className="management-form__grid">
              <Input
                label="Título"
                value={form.title}
                onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
                error={formError ? getFieldError(formError, 'Title') : undefined}
                required
              />
              <Input
                label="Categoría"
                value={form.category}
                onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))}
                error={formError ? getFieldError(formError, 'Category') : undefined}
                required
              />
              <Input
                label="Ubicación"
                value={form.location}
                onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))}
                error={formError ? getFieldError(formError, 'Location') : undefined}
                icon={<MapPin size={18} />}
                required
              />
              <PriceField
                label="Precio por persona (USD)"
                value={form.price}
                onChange={(event) => setForm((current) => ({
                  ...current,
                  price: Number(event.target.value),
                }))}
                error={formError ? getFieldError(formError, 'Price') : undefined}
                required
              />
              <Input
                label="Capacidad"
                type="number"
                min="1"
                step="1"
                value={form.capacity}
                onChange={(event) => setForm((current) => ({
                  ...current,
                  capacity: Number(event.target.value),
                }))}
                error={formError ? getFieldError(formError, 'Capacity') : undefined}
                icon={<UsersRound size={18} />}
                required
              />
            </div>
            <TextAreaField
              label="Descripción"
              value={form.description}
              onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              error={formError ? getFieldError(formError, 'Description') : undefined}
              rows={6}
              required
            />
            <div className="management-actions">
              <Button variant="outline" onClick={closeForm}>Cancelar</Button>
              <Button type="submit" isLoading={submitting}>Guardar borrador</Button>
            </div>
          </form>
        </section>
      )}

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
            <article className="management-card surface-panel" key={experience.id}>
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
                <div><dt>Cupos</dt><dd>{experience.availableSpots} de {experience.capacity}</dd></div>
              </dl>
              <div className="management-actions">
                {experience.approvalStatus !== 'Suspended' && (
                  <Button variant="outline" onClick={() => startEdit(experience)}>
                    <Pencil size={17} aria-hidden="true" />Editar
                  </Button>
                )}
                {(experience.approvalStatus === 'Draft' || experience.approvalStatus === 'Rejected') && (
                  <Button
                    onClick={() => void submitForReview(experience.id)}
                    isLoading={busyId === experience.id}
                  >
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
            </article>
          ))}
        </div>
      )}
    </div>
  );
};

export default HostExperiences;
