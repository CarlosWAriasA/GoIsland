import axios from 'axios';
import { ArrowLeft, CalendarPlus, Clock, Pencil, Trash2, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import { getFieldError, toApiError } from '../services/apiError';
import type { ApiError } from '../services/apiError';
import { hostExperienceService } from '../services/hostExperienceService';
import type { ExperienceSchedule } from '../types';

interface ScheduleForm { startsAt: string; endsAt: string; capacity: number; status: 'Scheduled' | 'Closed' }
const emptyForm: ScheduleForm = { startsAt: '', endsAt: '', capacity: 1, status: 'Scheduled' };
const toLocalInput = (value: string) => {
  const date = new Date(value);
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
};
const formatDate = (value: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'long', timeStyle: 'short',
}).format(new Date(value));

export const HostSchedules = () => {
  const experienceId = Number(useParams().id);
  const validExperienceId = Number.isInteger(experienceId) && experienceId > 0;
  const [schedules, setSchedules] = useState<ExperienceSchedule[]>([]);
  const [form, setForm] = useState<ScheduleForm>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [loading, setLoading] = useState(validExperienceId);
  const [submitting, setSubmitting] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    if (!validExperienceId) return;
    const controller = new AbortController();
    hostExperienceService.getSchedules(experienceId, controller.signal)
      .then(setSchedules)
      .catch((requestError: unknown) => {
        if (!axios.isCancel(requestError)) setError(toApiError(requestError).message);
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [experienceId, retry, validExperienceId]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true); setFormError(null); setSuccess(null);
    const payload = {
      startsAt: new Date(form.startsAt).toISOString(),
      endsAt: new Date(form.endsAt).toISOString(),
      capacity: form.capacity,
    };
    try {
      const saved = editingId
        ? await hostExperienceService.updateSchedule(editingId, { ...payload, status: form.status })
        : await hostExperienceService.createSchedule(experienceId, payload);
      setSchedules((current) => editingId
        ? current.map((item) => item.id === saved.id ? saved : item)
        : [...current, saved].sort((a, b) => a.startsAt.localeCompare(b.startsAt)));
      setForm(emptyForm); setEditingId(null);
      setSuccess(editingId ? 'Horario guardado.' : 'Horario publicado con disponibilidad real.');
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
    } finally { setSubmitting(false); }
  };

  const edit = (schedule: ExperienceSchedule) => {
    setEditingId(schedule.id);
    setForm({
      startsAt: toLocalInput(schedule.startsAt), endsAt: toLocalInput(schedule.endsAt),
      capacity: schedule.capacity,
      status: schedule.status === 'Closed' ? 'Closed' : 'Scheduled',
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const remove = async (schedule: ExperienceSchedule) => {
    if (!window.confirm('¿Eliminar este horario? Solo es posible si no tiene reservas.')) return;
    setBusyId(schedule.id); setError(null);
    try {
      await hostExperienceService.removeSchedule(schedule.id);
      setSchedules((current) => current.filter((item) => item.id !== schedule.id));
      setSuccess('Horario eliminado.');
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally { setBusyId(null); }
  };

  return (
    <div className="container management-page animate-fade-in">
      <Link className="reservation-detail__back" to="/host/experiences"><ArrowLeft size={18} /> Mis experiencias</Link>
      <header className="page-heading"><span className="page-heading__eyebrow">Organiza tus fechas</span>
        <h1>Horarios de la experiencia</h1><p>Publica nuevas fechas y decide cuántas personas pueden reservar.</p></header>
      {success && <Alert tone="success">{success}</Alert>}{error && <Alert tone="error">{error}</Alert>}

      <section className="management-form surface-panel" aria-labelledby="schedule-form-title">
        <div className="management-form__heading"><CalendarPlus /><div><h2 id="schedule-form-title">
          {editingId ? 'Editar horario' : 'Nuevo horario'}</h2><p>Usa la fecha y hora de tu dispositivo.</p></div></div>
        {formError && <Alert tone="error">{formError.message}</Alert>}
        <form onSubmit={submit}>
          <div className="management-form__grid">
            <Input label="Inicio" type="datetime-local" value={form.startsAt} min={toLocalInput(new Date().toISOString())}
              onChange={(event) => setForm((current) => ({ ...current, startsAt: event.target.value }))}
              error={formError ? getFieldError(formError, 'StartsAt') : undefined} required />
            <Input label="Final" type="datetime-local" value={form.endsAt}
              onChange={(event) => setForm((current) => ({ ...current, endsAt: event.target.value }))}
              error={formError ? getFieldError(formError, 'EndsAt') : undefined} required />
            <Input label="Capacidad" type="number" min="1" step="1" value={form.capacity}
              onChange={(event) => setForm((current) => ({ ...current, capacity: Number(event.target.value) }))}
              error={formError ? getFieldError(formError, 'Capacity') : undefined} required />
            {editingId && <SelectField label="Estado" value={form.status}
              onChange={(event) => setForm((current) => ({ ...current, status: event.target.value as ScheduleForm['status'] }))}>
              <option value="Scheduled">Abierto</option><option value="Closed">Cerrado</option>
            </SelectField>}
          </div>
          <div className="management-actions">
            {editingId && <Button variant="outline" onClick={() => { setEditingId(null); setForm(emptyForm); }}>Cancelar edición</Button>}
            <Button type="submit" isLoading={submitting}>{editingId ? 'Guardar horario' : 'Publicar horario'}</Button>
          </div>
        </form>
      </section>

      {!validExperienceId ? <EmptyState title="Experiencia no disponible" description="No pudimos abrir esta experiencia. Regresa a tus experiencias e inténtalo nuevamente." />
        : loading ? (
          <div className="management-list" role="status">
            {[1, 2].map((item) => <Skeleton key={item} className="management-card management-card--loading" />)}
            <span className="visually-hidden">Cargando horarios…</span>
          </div>
        ) : error && schedules.length === 0
        ? <ErrorState description={error} onRetry={() => { setLoading(true); setRetry((value) => value + 1); }} />
        : schedules.length === 0 ? <EmptyState title="Sin horarios" description="Publica la primera fecha disponible para habilitar reservas." />
          : <div className="management-list">{schedules.map((schedule) => (
            <article className="management-card surface-panel" key={schedule.id}>
              <div className="management-card__header"><div><span className="management-card__reference">Horario #{schedule.id}</span>
                <h2>{formatDate(schedule.startsAt)}</h2></div>
                <StatusBadge tone={schedule.status === 'Scheduled' ? 'success' : 'warning'}>{schedule.status === 'Scheduled' ? 'Abierto' : 'Cerrado'}</StatusBadge></div>
              <dl className="management-card__facts">
                <div><dt><Clock size={16} /> Finaliza</dt><dd>{formatDate(schedule.endsAt)}</dd></div>
                <div><dt><UsersRound size={16} /> Disponibles</dt><dd>{schedule.availableSpots} de {schedule.capacity}</dd></div>
              </dl>
              <div className="management-actions"><Button variant="outline" onClick={() => edit(schedule)}><Pencil size={17} /> Editar</Button>
                <Button variant="danger" onClick={() => void remove(schedule)} disabled={busyId === schedule.id}><Trash2 size={17} /> Eliminar</Button></div>
            </article>))}</div>}
    </div>
  );
};

export default HostSchedules;
