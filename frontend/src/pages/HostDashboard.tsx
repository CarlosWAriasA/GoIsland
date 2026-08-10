import axios from 'axios';
import { CalendarClock, CircleDollarSign, MapPinned, Star, TicketCheck, UsersRound } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Skeleton from '../components/Skeleton';
import { toApiError } from '../services/apiError';
import { hostDashboardService } from '../services/hostDashboardService';
import type { HostDashboard as HostDashboardData } from '../types';

const formatMoney = (amount: number, currency: string) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency,
}).format(amount);

const formatDate = (value: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium',
  timeStyle: 'short',
}).format(new Date(value));

export const HostDashboard = () => {
  const [dashboard, setDashboard] = useState<HostDashboardData | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    hostDashboardService.get(controller.signal)
      .then(setDashboard)
      .catch((requestError: unknown) => {
        if (!axios.isCancel(requestError)) setError(toApiError(requestError).message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [retry]);

  if (loading) {
    return (
      <div className="container host-dashboard" role="status" aria-busy="true">
        <Skeleton className="host-dashboard__loading" />
        <span className="visually-hidden">Cargando tu panel de anfitrión…</span>
      </div>
    );
  }
  if (error || !dashboard) {
    return <div className="container"><ErrorState description={error || 'No pudimos cargar tu panel.'}
      onRetry={() => {
        setError(null);
        setLoading(true);
        setRetry((current) => current + 1);
      }} /></div>;
  }

  const metrics = [
    { label: 'Experiencias publicadas', value: dashboard.publishedExperiences, icon: MapPinned },
    { label: 'Próximas reservas', value: dashboard.upcomingReservations, icon: TicketCheck },
    { label: 'Personas reservadas', value: dashboard.reservedSpots, icon: UsersRound },
    { label: 'Ingresos netos', value: formatMoney(dashboard.netEarnings, dashboard.currency), icon: CircleDollarSign },
    { label: 'Experiencias completadas', value: dashboard.completedReservations, icon: CalendarClock },
    {
      label: 'Calificación',
      value: dashboard.averageRating === null ? 'Sin reseñas' : `${dashboard.averageRating.toFixed(1)} / 5`,
      icon: Star,
    },
  ];

  return (
    <div className="container host-dashboard animate-fade-in">
      <header className="page-heading">
        <span className="page-heading__eyebrow">Tu actividad</span>
        <h1>Resumen del anfitrión</h1>
        <p>Tus publicaciones, reservas y pagos de un vistazo.</p>
      </header>
      <section className="dashboard-metrics" aria-label="Resumen de actividad">
        {metrics.map(({ label, value, icon: Icon }) => (
          <article className="dashboard-metric surface-panel" key={label}>
            <Icon aria-hidden="true" />
            <span>{label}</span>
            <strong>{value}</strong>
          </article>
        ))}
      </section>
      <section className="surface-panel dashboard-schedules" aria-labelledby="dashboard-schedules-title">
        <div className="dashboard-schedules__heading">
          <div>
            <h2 id="dashboard-schedules-title">Próximas fechas</h2>
            <p>{dashboard.upcomingSchedules} fecha{dashboard.upcomingSchedules === 1 ? '' : 's'} abierta{dashboard.upcomingSchedules === 1 ? '' : 's'}.</p>
          </div>
          <Link className="button-link button-link--outline" to="/host/experiences">Administrar</Link>
        </div>
        {dashboard.nextSchedules.length === 0 ? (
          <EmptyState title="Sin fechas próximas" description="Publica un horario para comenzar a recibir reservas." />
        ) : (
          <ol>
            {dashboard.nextSchedules.map((schedule) => (
              <li key={schedule.id}>
                <div>
                  <strong>{schedule.experienceTitle}</strong>
                  <span>{formatDate(schedule.startsAt)}</span>
                </div>
                <span>{schedule.reservedSpots} de {schedule.capacity} personas</span>
                <Link to={`/host/experiences/${schedule.experienceId}/schedules`}>Ver calendario</Link>
              </li>
            ))}
          </ol>
        )}
      </section>
    </div>
  );
};

export default HostDashboard;
