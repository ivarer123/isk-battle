import { useEffect, useState } from 'react';
import { ApiError } from '../api/client';
import { t, tryT } from '../i18n/t';

/** Shows a "waking up" hint if loading takes long (SQL auto-pause, ARCHITECTURE.md §10). */
export function Loading() {
  const [slow, setSlow] = useState(false);
  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), 3000);
    return () => clearTimeout(timer);
  }, []);
  return (
    <p className="muted center" role="status">
      {slow ? t('common.slowStart') : t('common.loading')}
    </p>
  );
}

export function errorMessage(error: unknown): string {
  return error instanceof ApiError && error.code ? tryT(`error.${error.code}`, t('common.error')) : t('common.error');
}

export function ErrorView({ error, onRetry }: { error?: unknown; onRetry?: () => void }) {
  return (
    <div className="card center" role="alert">
      <p>{errorMessage(error)}</p>
      {onRetry && <button onClick={onRetry}>{t('common.retry')}</button>}
    </div>
  );
}

export function FormError({ error }: { error: unknown }) {
  return error ? <p className="error" role="alert">{errorMessage(error)}</p> : null;
}
