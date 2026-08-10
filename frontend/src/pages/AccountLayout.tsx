import { BellRing, KeyRound, UserRound } from 'lucide-react';
import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

const sections = [
  { to: '/account', end: true, icon: UserRound, label: 'Perfil', hint: 'Tus datos personales' },
  { to: '/account/notifications', end: false, icon: BellRing, label: 'Avisos', hint: 'Dónde te avisamos' },
  { to: '/account/password', end: false, icon: KeyRound, label: 'Seguridad', hint: 'Tu contraseña' },
] as const;

export const AccountLayout = () => {
  const { user, authenticationMethod } = useAuth();
  // Con acceso por Google no hay contraseña que cambiar, así que la sección sobra.
  const usesGoogle = authenticationMethod === 'Google' || user?.hasPassword === false;
  const visibleSections = sections.filter(
    (section) => section.to !== '/account/password' || !usesGoogle,
  );

  return (
    <div className="container account-layout animate-fade-in">
      <header className="page-heading">
        <span className="page-heading__eyebrow">Tu cuenta</span>
        <h1>Configuración</h1>
        <p>Tus datos, tus avisos y el acceso a la cuenta, en un solo lugar.</p>
      </header>

      <div className="account-layout__body">
        <nav className="account-nav" aria-label="Secciones de la cuenta">
          {visibleSections.map((section) => {
            const Icon = section.icon;
            return (
              <NavLink
                key={section.to}
                to={section.to}
                end={section.end}
                className={({ isActive }) => `account-nav__item${isActive ? ' is-active' : ''}`}
              >
                <Icon size={19} aria-hidden="true" />
                <span>
                  <strong>{section.label}</strong>
                  <small>{section.hint}</small>
                </span>
              </NavLink>
            );
          })}
        </nav>

        <div className="account-layout__content">
          <Outlet />
        </div>
      </div>
    </div>
  );
};

export default AccountLayout;
