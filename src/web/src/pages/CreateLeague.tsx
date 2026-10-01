import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import type { Cadence } from '../api/client';
import { FormError } from '../components/Status';
import { useCreateLeague } from '../hooks/queries';
import { t } from '../i18n/t';

const cadences: Cadence[] = ['Daily', 'Weekly', 'Monthly'];

export function CreateLeague() {
  const [name, setName] = useState('');
  const [cadence, setCadence] = useState<Cadence>('Weekly');
  const create = useCreateLeague();
  const navigate = useNavigate();

  async function submit(event: FormEvent) {
    event.preventDefault();
    const league = await create.mutateAsync({ name, cadence }).catch(() => null);
    if (league) navigate(`/liga/${league.id}/installningar`);
  }

  return (
    <form className="stack" onSubmit={(e) => void submit(e)}>
      <h1>{t('create.title')}</h1>
      <div>
        <label htmlFor="league-name">{t('create.name')}</label>
        <input id="league-name" maxLength={60} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </div>
      <fieldset>
        <legend>{t('create.cadence')}</legend>
        {cadences.map((c) => (
          <label key={c} className="radio">
            <input type="radio" name="cadence" checked={cadence === c} onChange={() => setCadence(c)} />
            {t(`cadence.${c}`)}
          </label>
        ))}
      </fieldset>
      <FormError error={create.error} />
      <button type="submit" disabled={!name.trim() || create.isPending}>{t('create.submit')}</button>
    </form>
  );
}
