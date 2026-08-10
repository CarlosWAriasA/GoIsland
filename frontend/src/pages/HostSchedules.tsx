import axios from 'axios';
import {
  ArrowLeft,
  CalendarRange,
  CalendarPlus,
  Clock,
  Infinity as InfinityIcon,
  Pencil,
  Trash2,
  UsersRound,
} from 'lucide-react';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import ConfirmDialog from '../components/ConfirmDialog';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Input from '../components/Input';
import SelectField from '../components/SelectField';
import Skeleton from '../components/Skeleton';
import StatusBadge from '../components/StatusBadge';
import ToastFeedback from '../components/ToastFeedback';
import { getFieldError, toApiError } from '../services/apiError';
import type { ApiError } from '../services/apiError';
import { hostExperienceService } from '../services/hostExperienceService';
import type {
  ExperienceSchedule,
  ManagedExperience,
  CopyScheduleWeekRequest,
  RecurringSchedulePreview,
  RecurringScheduleRequest,
} from '../types';

interface ScheduleForm { startsAt: string; endsAt: string; capacity: number; status: 'Scheduled' | 'Closed' }
const emptyForm: ScheduleForm = { startsAt: '', endsAt: '', capacity: 1, status: 'Scheduled' };
const WEEKDAYS = [
  { value: 1, label: 'Lun' },
  { value: 2, label: 'Mar' },
  { value: 3, label: 'Mié' },
  { value: 4, label: 'Jue' },
  { value: 5, label: 'Vie' },
  { value: 6, label: 'Sáb' },
  { value: 0, label: 'Dom' },
];
const toDateInput = (date: Date) => {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 10);
};
const createRecurringForm = (): RecurringScheduleRequest => {
  const start = new Date();
  start.setDate(start.getDate() + 1);
  const end = new Date(start);
  end.setMonth(end.getMonth() + 3);
  return {
    startDate: toDateInput(start),
    endDate: toDateInput(end),
    startsAt: '09:00',
    endsAt: '11:00',
    weekdays: [start.getDay()],
    capacity: 1,
    excludedDates: [],
  };
};
const toMondayInput = (value: Date | string) => {
  const date = typeof value === 'string' ? new Date(`${value}T12:00:00`) : new Date(value);
  const offset = (date.getDay() + 6) % 7;
  date.setDate(date.getDate() - offset);
  return toDateInput(date);
};
const createCopyWeekForm = (): CopyScheduleWeekRequest => {
  const source = toMondayInput(new Date());
  const targetDate = new Date(`${source}T12:00:00`);
  targetDate.setDate(targetDate.getDate() + 7);
  return { sourceWeekStart: source, targetWeekStart: toDateInput(targetDate) };
};
const DEFAULT_TIME_ZONE = 'America/Santo_Domingo';
const getZonedParts = (value: Date, timeZone: string) => Object.fromEntries(
  new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
  }).formatToParts(value)
    .filter((part) => part.type !== 'literal')
    .map((part) => [part.type, part.value]),
);
const toLocalInput = (value: string, timeZone = DEFAULT_TIME_ZONE) => {
  const parts = getZonedParts(new Date(value), timeZone);
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`;
};
const toUtcFromLocal = (value: string, timeZone = DEFAULT_TIME_ZONE) => {
  const [datePart, timePart] = value.split('T');
  const [year, month, day] = datePart.split('-').map(Number);
  const [hour, minute] = timePart.split(':').map(Number);
  const expectedUtc = Date.UTC(year, month - 1, day, hour, minute);
  let candidate = new Date(expectedUtc);
  for (let attempt = 0; attempt < 2; attempt += 1) {
    const parts = getZonedParts(candidate, timeZone);
    const representedUtc = Date.UTC(
      Number(parts.year), Number(parts.month) - 1, Number(parts.day),
      Number(parts.hour), Number(parts.minute), Number(parts.second),
    );
    candidate = new Date(candidate.getTime() + expectedUtc - representedUtc);
  }
  return candidate.toISOString();
};
const formatDate = (value: string, timeZone = DEFAULT_TIME_ZONE) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'long', timeStyle: 'short', timeZone,
}).format(new Date(value));

export const HostSchedules = () => {
  const experienceId = Number(useParams().id);
  const validExperienceId = Number.isInteger(experienceId) && experienceId > 0;
  const [schedules, setSchedules] = useState<ExperienceSchedule[]>([]);
  const [experience, setExperience] = useState<ManagedExperience | null>(null);
  const [form, setForm] = useState<ScheduleForm>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [loading, setLoading] = useState(validExperienceId);
  const [submitting, setSubmitting] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<ApiError | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const [recurringForm, setRecurringForm] = useState<RecurringScheduleRequest>(createRecurringForm);
  const [excludedDate, setExcludedDate] = useState('');
  const [preview, setPreview] = useState<RecurringSchedulePreview | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [batchCapacity, setBatchCapacity] = useState(1);
  const [batchBusy, setBatchBusy] = useState(false);
  const [scheduleToDelete, setScheduleToDelete] = useState<ExperienceSchedule | null>(null);
  const [copyWeekForm, setCopyWeekForm] = useState<CopyScheduleWeekRequest>(createCopyWeekForm);
  const [copyPreview, setCopyPreview] = useState<RecurringSchedulePreview | null>(null);
  const [copyPreviewing, setCopyPreviewing] = useState(false);
  const [copying, setCopying] = useState(false);

  const selectableScheduleIds = schedules
    .filter((schedule) => new Date(schedule.startsAt) > new Date()
      && (schedule.status === 'Scheduled' || schedule.status === 'Closed'))
    .map((schedule) => schedule.id);

  useEffect(() => {
    if (!validExperienceId) return;
    const controller = new AbortController();
    Promise.all([
      hostExperienceService.getOne(experienceId, controller.signal),
      hostExperienceService.getSchedules(experienceId, controller.signal),
    ])
      .then(([loadedExperience, loadedSchedules]) => {
        setError(null);
        setExperience(loadedExperience);
        setSchedules(loadedSchedules);
        setBatchCapacity(loadedExperience.isUnlimitedCapacity ? 1 : loadedExperience.capacity);
        setRecurringForm((current) => ({
          ...current,
          capacity: loadedExperience.isUnlimitedCapacity ? 1 : loadedExperience.capacity,
        }));
      })
      .catch((requestError: unknown) => {
        if (!axios.isCancel(requestError)) setError(toApiError(requestError).message);
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [experienceId, retry, validExperienceId]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true); setFormError(null); setSuccess(null);
    const timeZone = experience?.timeZoneId ?? DEFAULT_TIME_ZONE;
    const payload = {
      startsAt: toUtcFromLocal(form.startsAt, timeZone),
      endsAt: toUtcFromLocal(form.endsAt, timeZone),
      capacity: experience?.isUnlimitedCapacity ? 1 : form.capacity,
    };
    try {
      const saved = editingId
        ? await hostExperienceService.updateSchedule(editingId, { ...payload, status: form.status })
        : await hostExperienceService.createSchedule(experienceId, payload);
      setSchedules((current) => editingId
        ? current.map((item) => item.id === saved.id ? saved : item)
        : [...current, saved].sort((a, b) => a.startsAt.localeCompare(b.startsAt)));
      setForm(emptyForm); setEditingId(null);
      setSuccess(editingId ? 'Horario guardado.' : 'Horario publicado.');
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
    } finally { setSubmitting(false); }
  };

  const edit = (schedule: ExperienceSchedule) => {
    setEditingId(schedule.id);
    const timeZone = experience?.timeZoneId ?? DEFAULT_TIME_ZONE;
    setForm({
      startsAt: toLocalInput(schedule.startsAt, timeZone),
      endsAt: toLocalInput(schedule.endsAt, timeZone),
      capacity: schedule.capacity,
      status: schedule.status === 'Closed' ? 'Closed' : 'Scheduled',
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const remove = async (schedule: ExperienceSchedule) => {
    setBusyId(schedule.id); setError(null);
    try {
      await hostExperienceService.removeSchedule(schedule.id);
      setSchedules((current) => current.filter((item) => item.id !== schedule.id));
      setSuccess('Horario eliminado.');
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally {
      setBusyId(null);
      setScheduleToDelete(null);
    }
  };

  const updateRecurring = (changes: Partial<RecurringScheduleRequest>) => {
    setRecurringForm((current) => ({ ...current, ...changes }));
    setPreview(null);
  };

  const recurringPayload = (): RecurringScheduleRequest => ({
    ...recurringForm,
    startsAt: recurringForm.startsAt.length === 5 ? `${recurringForm.startsAt}:00` : recurringForm.startsAt,
    endsAt: recurringForm.endsAt.length === 5 ? `${recurringForm.endsAt}:00` : recurringForm.endsAt,
    capacity: experience?.isUnlimitedCapacity ? 1 : recurringForm.capacity,
  });

  const previewRecurring = async () => {
    setPreviewing(true); setFormError(null); setSuccess(null);
    try {
      setPreview(await hostExperienceService.previewRecurringSchedules(experienceId, recurringPayload()));
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
    } finally { setPreviewing(false); }
  };

  const generateRecurring = async () => {
    if (!preview) return;
    setGenerating(true); setFormError(null); setSuccess(null);
    try {
      const result = await hostExperienceService.generateRecurringSchedules(experienceId, recurringPayload());
      setSuccess(result.created > 0
        ? `${result.created} ${result.created === 1 ? 'horario creado' : 'horarios creados'}.`
        : 'El calendario ya contenía todos estos horarios.');
      setPreview(null);
      setSelectedIds([]);
      setRetry((value) => value + 1);
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
    } finally { setGenerating(false); }
  };

  const replaceBatch = (updated: ExperienceSchedule[]) => {
    const byId = new Map(updated.map((item) => [item.id, item]));
    setSchedules((current) => current.map((item) => byId.get(item.id) ?? item));
    setSelectedIds([]);
  };

  const closeSelected = async () => {
    setBatchBusy(true); setError(null); setSuccess(null);
    try {
      const result = await hostExperienceService.closeSchedules(experienceId, selectedIds);
      replaceBatch(result.schedules);
      setSuccess(`${result.schedules.length} ${result.schedules.length === 1 ? 'horario cerrado' : 'horarios cerrados'}.`);
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally { setBatchBusy(false); }
  };

  const updateSelectedCapacity = async () => {
    setBatchBusy(true); setError(null); setSuccess(null);
    try {
      const result = await hostExperienceService.updateSchedulesCapacity(
        experienceId,
        selectedIds,
        batchCapacity,
      );
      replaceBatch(result.schedules);
      setSuccess(`Capacidad actualizada en ${result.schedules.length} ${result.schedules.length === 1 ? 'horario' : 'horarios'}.`);
    } catch (requestError: unknown) { setError(toApiError(requestError).message); }
    finally { setBatchBusy(false); }
  };

  const updateCopyWeek = (changes: Partial<CopyScheduleWeekRequest>) => {
    setCopyWeekForm((current) => ({ ...current, ...changes }));
    setCopyPreview(null);
  };

  const previewCopyWeek = async () => {
    setCopyPreviewing(true); setFormError(null); setSuccess(null);
    try {
      setCopyPreview(await hostExperienceService.previewCopyWeek(experienceId, copyWeekForm));
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
    } finally { setCopyPreviewing(false); }
  };

  const copyWeek = async () => {
    if (!copyPreview) return;
    setCopying(true); setFormError(null); setSuccess(null);
    try {
      const result = await hostExperienceService.copyScheduleWeek(experienceId, copyWeekForm);
      setSuccess(result.created > 0
        ? `${result.created} ${result.created === 1 ? 'horario copiado' : 'horarios copiados'}.`
        : 'La semana de destino ya contenía estos horarios.');
      setCopyPreview(null);
      setSelectedIds([]);
      setRetry((value) => value + 1);
    } catch (requestError: unknown) {
      setFormError(toApiError(requestError));
    } finally { setCopying(false); }
  };

  return (
    <div className="container management-page animate-fade-in">
      <Link className="reservation-detail__back" to="/host/experiences"><ArrowLeft size={18} /> Mis experiencias</Link>
      <header className="page-heading"><span className="page-heading__eyebrow">Organiza tus fechas</span>
        <h1>Horarios de la experiencia</h1><p>Publica nuevas fechas y decide cuántas personas pueden reservar.</p></header>
      <ToastFeedback message={success} tone="success" />
      <ToastFeedback message={error} tone="error" />

      <section className="management-form surface-panel" aria-labelledby="schedule-form-title">
        <div className="management-form__heading"><CalendarPlus /><div><h2 id="schedule-form-title">
          {editingId ? 'Editar horario' : 'Nuevo horario'}</h2><p>Usa la hora local de la experiencia.</p></div></div>
        <ToastFeedback message={formError?.message} tone="error" />
        <form onSubmit={submit}>
          <div className="management-form__grid">
            <Input label="Inicio" type="datetime-local" value={form.startsAt}
              min={toLocalInput(new Date().toISOString(), experience?.timeZoneId)}
              onChange={(event) => setForm((current) => ({ ...current, startsAt: event.target.value }))}
              error={formError ? getFieldError(formError, 'StartsAt') : undefined} required />
            <Input label="Final" type="datetime-local" value={form.endsAt}
              onChange={(event) => setForm((current) => ({ ...current, endsAt: event.target.value }))}
              error={formError ? getFieldError(formError, 'EndsAt') : undefined} required />
            {experience?.isUnlimitedCapacity ? (
              <Alert tone="info"><InfinityIcon /> Esta experiencia no tiene límite de personas.</Alert>
            ) : (
              <Input label="Capacidad" type="number" min="1" step="1" value={form.capacity}
                onChange={(event) => setForm((current) => ({ ...current, capacity: Number(event.target.value) }))}
                error={formError ? getFieldError(formError, 'Capacity') : undefined} required />
            )}
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

      <details className="experience-advanced schedule-recurring surface-panel">
        <summary><CalendarRange size={19} aria-hidden="true" />Crear varios horarios</summary>
        <div className="experience-advanced__content">
          <p>Elige un rango, los días de la semana y revisa las fechas antes de crearlas.</p>
          <div className="management-form__grid">
            <Input label="Desde" type="date" value={recurringForm.startDate}
              min={toDateInput(new Date())}
              onChange={(event) => updateRecurring({ startDate: event.target.value })} />
            <Input label="Hasta" type="date" value={recurringForm.endDate}
              min={recurringForm.startDate}
              onChange={(event) => updateRecurring({ endDate: event.target.value })} />
            <Input label="Hora de inicio" type="time" value={recurringForm.startsAt}
              onChange={(event) => updateRecurring({ startsAt: event.target.value })} />
            <Input label="Hora final" type="time" value={recurringForm.endsAt}
              onChange={(event) => updateRecurring({ endsAt: event.target.value })} />
            {!experience?.isUnlimitedCapacity && <Input label="Capacidad" type="number" min="1"
              value={recurringForm.capacity}
              onChange={(event) => updateRecurring({ capacity: Number(event.target.value) })} />}
          </div>
          <fieldset className="schedule-weekdays">
            <legend>Días de la semana</legend>
            {WEEKDAYS.map((day) => <label key={day.value}>
              <input type="checkbox" checked={recurringForm.weekdays.includes(day.value)}
                onChange={(event) => updateRecurring({
                  weekdays: event.target.checked
                    ? [...recurringForm.weekdays, day.value]
                    : recurringForm.weekdays.filter((value) => value !== day.value),
                })} />
              <span>{day.label}</span>
            </label>)}
          </fieldset>
          <div className="schedule-exclusions">
            <Input label="Excluir una fecha" type="date" value={excludedDate}
              min={recurringForm.startDate} max={recurringForm.endDate}
              onChange={(event) => setExcludedDate(event.target.value)} />
            <Button type="button" variant="outline" disabled={!excludedDate}
              onClick={() => {
                if (!excludedDate) return;
                updateRecurring({
                  excludedDates: Array.from(new Set([
                    ...recurringForm.excludedDates,
                    excludedDate,
                  ])).sort(),
                });
                setExcludedDate('');
              }}>Excluir fecha</Button>
          </div>
          {recurringForm.excludedDates.length > 0 && <ul className="schedule-exclusion-list">
            {recurringForm.excludedDates.map((date) => <li key={date}>
              <span>{new Date(`${date}T12:00:00`).toLocaleDateString('es-DO', { dateStyle: 'long' })}</span>
              <button type="button" onClick={() => updateRecurring({
                excludedDates: recurringForm.excludedDates.filter((value) => value !== date),
              })}>Incluir nuevamente</button>
            </li>)}
          </ul>}
          <div className="management-actions">
            <Button type="button" variant="outline" onClick={() => void previewRecurring()}
              isLoading={previewing}>Ver fechas</Button>
            {preview && <Button type="button" onClick={() => void generateRecurring()}
              isLoading={generating} disabled={preview.toCreate === 0}>Crear horarios</Button>}
          </div>
          {preview && <div className="schedule-preview" aria-live="polite">
            <Alert tone="info">
              {preview.toCreate} por crear · {preview.existing} existentes · {preview.excluded} excluidos
            </Alert>
            <p>Las horas corresponden al destino de la experiencia.</p>
            <ol>{preview.items.map((item) => <li key={item.localDate + item.startsAt}>
              <time dateTime={item.startsAt}>{formatDate(item.startsAt, experience?.timeZoneId)}</time>
              <span>{item.disposition === 'WillCreate' ? 'Se creará'
                : item.disposition === 'Existing' ? 'Ya existe' : 'Excluido'}</span>
            </li>)}</ol>
          </div>}
        </div>
      </details>

      <details className="experience-advanced schedule-recurring surface-panel">
        <summary><CalendarRange size={19} aria-hidden="true" />Copiar una semana</summary>
        <div className="experience-advanced__content">
          <p>Repite los días, horas y capacidades de una semana.</p>
          <div className="management-form__grid">
            <Input label="Semana de origen" type="date" value={copyWeekForm.sourceWeekStart}
              onChange={(event) => updateCopyWeek({ sourceWeekStart: toMondayInput(event.target.value) })} />
            <Input label="Semana de destino" type="date" value={copyWeekForm.targetWeekStart}
              min={toMondayInput(new Date())}
              onChange={(event) => updateCopyWeek({ targetWeekStart: toMondayInput(event.target.value) })} />
          </div>
          <div className="management-actions">
            <Button type="button" variant="outline" onClick={() => void previewCopyWeek()}
              isLoading={copyPreviewing}>Revisar semana</Button>
            {copyPreview && <Button type="button" onClick={() => void copyWeek()}
              isLoading={copying} disabled={copyPreview.toCreate === 0}>Copiar horarios</Button>}
          </div>
          {copyPreview && <div className="schedule-preview" aria-live="polite">
            <Alert tone="info">{copyPreview.items.length === 0
              ? 'La semana de origen no tiene horarios para copiar.'
              : `${copyPreview.toCreate} por copiar · ${copyPreview.existing} ya existentes`}</Alert>
            {copyPreview.items.length > 0 && <ol>{copyPreview.items.map((item) => (
              <li key={item.localDate + item.startsAt}>
                <time dateTime={item.startsAt}>{formatDate(item.startsAt, experience?.timeZoneId)}</time>
                <span>{item.disposition === 'WillCreate' ? 'Se copiará' : 'Ya existe'}</span>
              </li>
            ))}</ol>}
          </div>}
        </div>
      </details>

      {selectedIds.length > 0 && <section className="schedule-batch surface-panel" aria-label="Acciones para horarios seleccionados">
        <strong>{selectedIds.length} {selectedIds.length === 1 ? 'horario seleccionado' : 'horarios seleccionados'}</strong>
        {!experience?.isUnlimitedCapacity && <Input label="Nueva capacidad" type="number" min="1"
          value={batchCapacity} onChange={(event) => setBatchCapacity(Number(event.target.value))} />}
        {!experience?.isUnlimitedCapacity && <Button variant="outline" isLoading={batchBusy}
          onClick={() => void updateSelectedCapacity()}>Actualizar capacidad</Button>}
        <Button variant="danger" isLoading={batchBusy} onClick={() => void closeSelected()}>Cerrar horarios</Button>
      </section>}

      {!loading && selectableScheduleIds.length > 0 && <div className="schedule-selection-toolbar">
        <span>Selecciona fechas para actualizar varias a la vez.</span>
        <Button type="button" variant="outline" onClick={() => setSelectedIds(
          selectedIds.length > 0 ? [] : selectableScheduleIds.slice(0, 200),
        )}>{selectedIds.length > 0 ? 'Quitar selección' : 'Seleccionar próximas'}</Button>
      </div>}

      {!validExperienceId ? <EmptyState title="Experiencia no disponible" description="No pudimos abrir esta experiencia. Regresa a tus experiencias e inténtalo nuevamente." />
        : loading ? (
          <div className="management-list" role="status">
            {[1, 2].map((item) => <Skeleton key={item} className="management-card management-card--loading" />)}
            <span className="visually-hidden">Cargando horarios…</span>
          </div>
        ) : error && schedules.length === 0
        ? <ErrorState description={error} onRetry={() => {
          setError(null);
          setLoading(true);
          setRetry((value) => value + 1);
        }} />
        : schedules.length === 0 ? <EmptyState title="Sin horarios" description="Publica la primera fecha disponible para habilitar reservas." />
          : <div className="management-list">{schedules.map((schedule) => (
            <article className="management-card surface-panel" key={schedule.id}>
              <div className="management-card__header"><div className="schedule-card-heading">
                {selectableScheduleIds.includes(schedule.id) && <label className="schedule-selector">
                  <input type="checkbox" checked={selectedIds.includes(schedule.id)}
                    onChange={(event) => setSelectedIds((current) => event.target.checked
                      ? [...current, schedule.id]
                      : current.filter((id) => id !== schedule.id))} />
                  <span className="visually-hidden">Seleccionar {formatDate(schedule.startsAt, experience?.timeZoneId)}</span>
                </label>}
                <div><span className="management-card__reference">Horario #{schedule.id}</span>
                  <h2>{formatDate(schedule.startsAt, experience?.timeZoneId)}</h2></div></div>
                <StatusBadge tone={schedule.status === 'Scheduled' ? 'success' : 'warning'}>{schedule.status === 'Scheduled' ? 'Abierto' : 'Cerrado'}</StatusBadge></div>
              <dl className="management-card__facts">
                <div><dt><Clock size={16} /> Finaliza</dt><dd>{formatDate(schedule.endsAt, experience?.timeZoneId)}</dd></div>
                <div>
                  <dt><UsersRound size={16} /> Disponibles</dt>
                  <dd>{schedule.isUnlimitedCapacity ? 'Sin límite' : `${schedule.availableSpots} de ${schedule.capacity}`}</dd>
                </div>
              </dl>
              <div className="management-actions"><Button variant="outline" onClick={() => edit(schedule)}><Pencil size={17} /> Editar</Button>
                <Button variant="danger" onClick={() => setScheduleToDelete(schedule)} disabled={busyId === schedule.id}><Trash2 size={17} /> Eliminar</Button></div>
            </article>))}</div>}
      <ConfirmDialog
        open={scheduleToDelete !== null}
        title="Eliminar horario"
        message="¿Quieres eliminar este horario? Solo es posible si no tiene reservas."
        confirmLabel="Eliminar horario"
        isConfirming={busyId !== null}
        onClose={() => setScheduleToDelete(null)}
        onConfirm={() => {
          if (scheduleToDelete) void remove(scheduleToDelete);
        }}
      />
    </div>
  );
};

export default HostSchedules;
