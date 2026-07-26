import axios from 'axios';
import { Bell, Check, Mail, Smartphone } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import Alert from '../components/Alert';
import Button from '../components/Button';
import EmptyState from '../components/EmptyState';
import ErrorState from '../components/ErrorState';
import Skeleton from '../components/Skeleton';
import { toApiError } from '../services/apiError';
import { notificationService } from '../services/notificationService';
import {
  activateWebPush,
  deactivateWebPush,
  getWebPushStatus,
  type WebPushStatus,
} from '../services/webPushService';
import type { NotificationItem, NotificationPreferences } from '../types';

const formatDate = (date: string) => new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(date));

export const Notifications = () => {
  const [items, setItems] = useState<NotificationItem[] | null>(null);
  const [preferences, setPreferences] = useState<NotificationPreferences | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [pushStatus, setPushStatus] = useState<WebPushStatus | 'checking'>('checking');
  const [updatingPush, setUpdatingPush] = useState(false);
  const [pushFeedback, setPushFeedback] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      notificationService.getAll(controller.signal),
      notificationService.getPreferences(controller.signal),
    ]).then(([notifications, currentPreferences]) => {
      setItems(notifications);
      setPreferences(currentPreferences);
    }).catch((requestError: unknown) => {
      if (!axios.isCancel(requestError)) setError(toApiError(requestError, 'No fue posible cargar las notificaciones.').message);
    });
    void getWebPushStatus().then(setPushStatus, () => setPushStatus('unsupported'));
    return () => controller.abort();
  }, []);

  const markRead = async (id: number) => {
    try {
      const updated = await notificationService.markRead(id);
      setItems((current) => current?.map((item) => item.id === id ? updated : item) ?? current);
    } catch (requestError: unknown) {
      setError(toApiError(requestError, 'No fue posible marcar la notificacion.').message);
    }
  };

  const savePreferences = async () => {
    if (!preferences) return;
    setSaving(true);
    setSaved(false);
    try {
      setPreferences(await notificationService.updatePreferences(preferences));
      setSaved(true);
    } catch (requestError: unknown) {
      setError(toApiError(requestError, 'No fue posible guardar las preferencias.').message);
    } finally { setSaving(false); }
  };

  const activatePushForDevice = async () => {
    if (!preferences) return;
    setUpdatingPush(true);
    setPushFeedback(null);
    try {
      const status = await activateWebPush();
      setPushStatus(status);
      if (status === 'active') {
        setPreferences(await notificationService.updatePreferences({ ...preferences, pushEnabled: true }));
        setPushFeedback('Los avisos quedaron activados en este dispositivo.');
      } else if (status === 'denied') {
        setPushFeedback('Los avisos están bloqueados. Puedes habilitarlos desde la configuración del sitio.');
      }
    } catch (requestError: unknown) {
      setError(toApiError(requestError, 'No fue posible activar los avisos en este dispositivo.').message);
    } finally {
      setUpdatingPush(false);
    }
  };

  const deactivatePushForDevice = async () => {
    if (!preferences) return;
    setUpdatingPush(true);
    setPushFeedback(null);
    try {
      await deactivateWebPush();
      setPushStatus('inactive');
      setPreferences(await notificationService.updatePreferences({ ...preferences, pushEnabled: false }));
      setPushFeedback('Los avisos se desactivaron en este dispositivo.');
    } catch (requestError: unknown) {
      setPushStatus(await getWebPushStatus());
      setError(toApiError(requestError, 'No fue posible desactivar los avisos en este dispositivo.').message);
    } finally {
      setUpdatingPush(false);
    }
  };

  if (error && !items) return <div className="container management-page"><ErrorState description={error} /></div>;
  if (!items || !preferences) return <div className="container management-page" role="status"><Skeleton className="management-summary" /></div>;

  return (
    <main className="container management-page animate-fade-in">
      <header className="page-heading"><span className="page-heading__eyebrow">Tu actividad</span><h1>Notificaciones</h1>
        <p>Aquí encontrarás novedades sobre tus reservas, pagos y experiencias.</p></header>
      {error && <Alert tone="error">{error}</Alert>}
      {saved && <Alert tone="success">Preferencias guardadas.</Alert>}
      {pushFeedback && <Alert tone={pushStatus === 'active' ? 'success' : 'warning'}>{pushFeedback}</Alert>}

      <section className="surface-panel notification-preferences" aria-labelledby="notification-preferences-title">
        <h2 id="notification-preferences-title">Preferencias</h2>
        {([
          ['dashboardEnabled', Bell, 'En la aplicación', 'Mostrar tus avisos en esta página.'],
          ['emailEnabled', Mail, 'Por correo electrónico', 'Enviar tus avisos al correo de tu cuenta.'],
          ['pushEnabled', Smartphone, 'En tus dispositivos', 'Recibir avisos aunque no tengas GoIsland abierto.'],
        ] as const).map(([key, Icon, label, description]) => (
          <label className="notification-toggle" key={key}>
            <input type="checkbox" checked={preferences[key]}
              onChange={(event) => setPreferences({ ...preferences, [key]: event.target.checked })} />
            <Icon aria-hidden="true" /><span><strong>{label}</strong><small>{description}</small></span>
          </label>
        ))}
        <div className="push-device-control">
          <div>
            <strong>Este dispositivo</strong>
            <small>
              {pushStatus === 'checking' && 'Comprobando si los avisos están activados...'}
              {pushStatus === 'active' && 'Los avisos están activados en este dispositivo.'}
              {pushStatus === 'inactive' && 'Los avisos no están activados en este dispositivo.'}
              {pushStatus === 'denied' && 'Los avisos están bloqueados en la configuración del sitio.'}
              {pushStatus === 'unsupported' && 'Este dispositivo no permite recibir avisos fuera de la aplicación.'}
            </small>
          </div>
          {pushStatus === 'active' ? (
            <Button variant="outline" onClick={() => void deactivatePushForDevice()} isLoading={updatingPush}>
              Desactivar avisos
            </Button>
          ) : (
            <Button onClick={() => void activatePushForDevice()} isLoading={updatingPush}
              disabled={pushStatus === 'checking' || pushStatus === 'denied' || pushStatus === 'unsupported'}>
              Activar avisos
            </Button>
          )}
        </div>
        <Button onClick={() => void savePreferences()} isLoading={saving}>Guardar preferencias</Button>
      </section>

      <section className="notification-list" aria-labelledby="notification-list-title">
        <h2 id="notification-list-title">Actividad reciente</h2>
        {items.length === 0 ? <EmptyState title="Aun no hay notificaciones" description="Los cambios de tus reservas apareceran aqui." /> : (
          <ol>{items.map((item) => (
            <li className={`surface-card notification-item${item.readAt ? '' : ' notification-item--unread'}`} key={item.id}>
              <div><span className="notification-item__date">{formatDate(item.createdAt)}</span><h3>{item.title}</h3><p>{item.message}</p>
                {item.actionUrl && <Link to={item.actionUrl}>Ver detalle</Link>}</div>
              {!item.readAt && <Button variant="ghost" size="sm" onClick={() => void markRead(item.id)}><Check size={17} /> Marcar leida</Button>}
            </li>
          ))}</ol>
        )}
      </section>
    </main>
  );
};

export default Notifications;
