import { useState, type FormEvent } from 'react';
import { useCheckIn } from '../hooks/queries';
import { t } from '../i18n/t';
import { decimals, formatPercent, MAX_YTD, MIN_YTD, parsePercent } from '../lib/numbers';
import { FormError } from './Status';

/** Check-in with the >20 pp typo guard (GAME-DESIGN.md §4). */
export function CheckInDialog({ initial, onClose }: { initial?: number; onClose: () => void }) {
  const [text, setText] = useState(initial === undefined ? '' : String(initial).replace('.', ','));
  const [validation, setValidation] = useState<string | null>(null);
  const [confirmFrom, setConfirmFrom] = useState<number | null>(null);
  const checkIn = useCheckIn();
  const value = parsePercent(text);

  async function submit(confirmed: boolean) {
    if (value === null || value < MIN_YTD || value > MAX_YTD) return setValidation(t('checkIn.invalid'));
    if (decimals(value) > 4) return setValidation(t('checkIn.tooPrecise'));
    setValidation(null);
    const result = await checkIn.mutateAsync({ ytdPercent: value, confirmed });
    if (result.kind === 'confirm') setConfirmFrom(result.previousYtdPercent);
    else onClose();
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    void submit(false).catch(() => undefined);
  }

  return (
    <div className="backdrop" role="dialog" aria-modal="true" aria-labelledby="checkin-title">
      <div className="dialog">
        {confirmFrom === null || value === null ? (
          <form onSubmit={onSubmit}>
            <h2 id="checkin-title">{t('checkIn.title')}</h2>
            <label htmlFor="ytd">{t('checkIn.label')}</label>
            <input
              id="ytd"
              inputMode="decimal"
              autoComplete="off"
              autoFocus
              placeholder={t('checkIn.hint')}
              value={text}
              onChange={(e) => setText(e.target.value)}
            />
            {validation && <p className="error" role="alert">{validation}</p>}
            <FormError error={checkIn.error} />
            <div className="actions">
              <button type="button" className="secondary" onClick={onClose}>{t('common.cancel')}</button>
              <button type="submit" disabled={checkIn.isPending}>{t('checkIn.submit')}</button>
            </div>
          </form>
        ) : (
          <div>
            <h2 id="checkin-title">{t('checkIn.confirmTitle')}</h2>
            <p>{t('checkIn.confirmText', { previous: formatPercent(confirmFrom), value: formatPercent(value) })}</p>
            <FormError error={checkIn.error} />
            <div className="actions">
              <button type="button" className="secondary" onClick={() => setConfirmFrom(null)}>{t('checkIn.edit')}</button>
              <button type="button" disabled={checkIn.isPending} onClick={() => void submit(true).catch(() => undefined)}>
                {t('checkIn.confirm')}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
