import { useEffect, useRef } from 'react';
import { Link, useParams } from 'react-router';
import { ApiError } from '../api/client';
import { ErrorView, Loading } from '../components/Status';
import { useJoinLeague } from '../hooks/queries';
import { t } from '../i18n/t';

/** Screen 8: joining via invite link. Active or "Väntar på plats". */
export function Join() {
  const { code = '' } = useParams();
  const join = useJoinLeague();
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    join.mutate(code);
  }, [code, join]);

  if (join.isError) {
    return join.error instanceof ApiError && join.error.status === 404 ? (
      <div className="card"><p>{t('join.notFound')}</p></div>
    ) : (
      <ErrorView error={join.error} onRetry={() => join.mutate(code)} />
    );
  }
  if (!join.data) return <Loading />;

  const { leagueId, leagueName, status } = join.data;
  return (
    <div className="stack">
      <h1>{t('join.title')}</h1>
      <div className="card">
        <p>{status === 'Active' ? t('join.joined', { league: leagueName }) : t('join.pending', { league: leagueName })}</p>
        <Link className="button" to={`/liga/${leagueId}`}>{t('join.open')}</Link>
      </div>
    </div>
  );
}
