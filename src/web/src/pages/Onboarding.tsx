import { useState, type FormEvent } from 'react';
import { Layout } from '../components/Layout';
import { FormError } from '../components/Status';
import { useUpdateMe } from '../hooks/queries';
import { t } from '../i18n/t';

/** Screen 2: display name + which account to use. */
export function Onboarding() {
  const [name, setName] = useState('');
  const update = useUpdateMe();

  function submit(event: FormEvent) {
    event.preventDefault();
    update.mutate({ displayName: name });
  }

  return (
    <Layout>
      <form className="stack" onSubmit={submit}>
        <h1>{t('onboarding.title')}</h1>
        <div className="card">
          <h2>{t('onboarding.accountTitle')}</h2>
          <p>{t('onboarding.accountText')}</p>
        </div>
        <div>
          <label htmlFor="name">{t('onboarding.displayName')}</label>
          <input id="name" maxLength={50} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          <p className="muted small">{t('onboarding.displayNameHint')}</p>
        </div>
        <FormError error={update.error} />
        <button type="submit" disabled={!name.trim() || update.isPending}>{t('onboarding.continue')}</button>
      </form>
    </Layout>
  );
}
