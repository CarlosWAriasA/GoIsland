import { Link } from 'react-router-dom';
import EmptyState from '../components/EmptyState';
import { usePageMetadata } from '../hooks/usePageMetadata';

export const NotFound = () => {
  usePageMetadata({
    title: 'Página no encontrada | GoIsland',
    description: 'La página que buscas no está disponible.',
    path: '/404',
  });

  return (
    <div className="container animate-fade-in">
      <EmptyState
        title="No encontramos esta página"
        description="Es posible que el enlace haya cambiado o ya no esté disponible."
        action={<Link className="button-link" to="/experiences">Explorar experiencias</Link>}
      />
    </div>
  );
};

export default NotFound;
