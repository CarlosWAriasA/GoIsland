import { Link } from 'react-router-dom';
import Logo from './Logo';

export const Footer = () => (
  <footer className="site-footer">
    <div className="container site-footer__inner">
      <div className="site-footer__brand">
        <Logo fontSize="1.45rem" variant="light" />
        <p>Experiencias locales con precios y disponibilidad real en República Dominicana.</p>
      </div>

      <nav className="site-footer__nav" aria-label="Navegación secundaria">
        <Link to="/experiences">Explorar experiencias</Link>
        <Link to="/login">Iniciar sesión</Link>
        <Link to="/register">Crear cuenta</Link>
      </nav>

      <p className="site-footer__copyright">© 2026 GoIsland</p>
    </div>
  </footer>
);

export default Footer;
