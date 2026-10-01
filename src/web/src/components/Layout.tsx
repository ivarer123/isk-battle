import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { t } from '../i18n/t';

export function Layout({ children, signedIn = false }: { children: ReactNode; signedIn?: boolean }) {
  return (
    <div className="app">
      <header className="topbar">
        <Link to="/" className="brand">{t('app.name')}</Link>
        {signedIn && (
          <nav>
            <Link to="/profil">{t('nav.profile')}</Link>
          </nav>
        )}
      </header>
      <main className="content">{children}</main>
      <footer className="footer">
        <Link to="/villkor">{t('footer.terms')}</Link>
        <Link to="/integritet">{t('footer.privacy')}</Link>
      </footer>
    </div>
  );
}
