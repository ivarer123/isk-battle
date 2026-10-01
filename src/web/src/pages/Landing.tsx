import { useState, type FormEvent } from 'react';
import { useAuth } from '../auth/AuthContext';
import { Layout } from '../components/Layout';
import { t } from '../i18n/t';

/** Screen 1: landing / sign-in. Rendered at the requested URL, so deep links (invites) survive sign-in. */
export function Landing({ returnTo }: { returnTo: string }) {
  const auth = useAuth();
  const [name, setName] = useState('');

  function devSignIn(event: FormEvent) {
    event.preventDefault();
    void auth.signIn(name);
  }

  return (
    <Layout>
      <section className="hero">
        <h1>{t('app.name')}</h1>
        <p className="lead">{t('landing.tagline')}</p>
        <ul className="points">
          <li>{t('landing.points.1')}</li>
          <li>{t('landing.points.2')}</li>
          <li>{t('landing.points.3')}</li>
        </ul>
        {auth.mode === 'dev' ? (
          <form onSubmit={devSignIn} className="card" data-return-to={returnTo}>
            <label htmlFor="dev-name">{t('landing.devName')}</label>
            <input id="dev-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
            <button type="submit" disabled={!name.trim()}>{t('landing.devSignIn')}</button>
          </form>
        ) : (
          <button className="big" onClick={() => void auth.signIn()}>{t('landing.signIn')}</button>
        )}
      </section>
    </Layout>
  );
}
