import { Link } from 'react-router-dom';
import Logo from './Logo';

export const Footer = () => (
  <footer className="site-footer">
    <div className="container site-footer__inner">
      <Logo fontSize="1.1rem" />

      <nav className="site-footer__nav" aria-label="Navegación secundaria">
        <Link to="/">Inicio</Link>
        <Link to="/experiences">Experiencias</Link>
        <Link to="/host-profile">Ser anfitrión</Link>
      </nav>

      <p className="site-footer__copyright">© 2026 GoIsland</p>
    </div>
  </footer>
);

export default Footer;
