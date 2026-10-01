import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth/AuthContext';
import { FormError, Loading } from '../components/Status';
import { useDeleteMe, useMe, useUpdateMe } from '../hooks/queries';
import { t } from '../i18n/t';

/** Screen 9: display name, reminders, sign out, delete account. */
export function Profile() {
  const me = useMe();
  if (!me.data) return <Loading />;
  return <ProfileForm displayName={me.data.displayName} reminders={me.data.emailRemindersEnabled} />;
}

function ProfileForm({ displayName, reminders }: { displayName: string; reminders: boolean }) {
  const auth = useAuth();
  const queryClient = useQueryClient();
  const update = useUpdateMe();
  const deleteMe = useDeleteMe();
  const [name, setName] = useState(displayName);

  async function signOut() {
    queryClient.clear();
    await auth.signOut();
  }

  async function deleteAccount() {
    if (!window.confirm(t('profile.deleteConfirm'))) return;
    await deleteMe.mutateAsync();
    await signOut();
  }

  return (
    <div className="stack">
      <h1>{t('profile.title')}</h1>
      <div>
        <label htmlFor="name">{t('profile.displayName')}</label>
        <div className="inline">
          <input id="name" maxLength={50} value={name} onChange={(e) => setName(e.target.value)} />
          <button disabled={!name.trim() || name === displayName} onClick={() => update.mutate({ displayName: name })}>
            {t('common.save')}
          </button>
        </div>
      </div>
      <label className="checkbox">
        <input
          type="checkbox"
          checked={reminders}
          onChange={(e) => update.mutate({ emailRemindersEnabled: e.target.checked })}
        />
        <span>
          {t('profile.reminders')}
          <br />
          <span className="muted small">{t('profile.remindersHint')}</span>
        </span>
      </label>
      <FormError error={update.error ?? deleteMe.error} />
      <section className="danger-zone">
        <button className="secondary" onClick={() => void signOut()}>{t('profile.signOut')}</button>
        <button className="danger" onClick={() => void deleteAccount().catch(() => undefined)}>{t('profile.delete')}</button>
      </section>
    </div>
  );
}
