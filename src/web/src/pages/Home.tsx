import { useState } from 'react';
import { Link } from 'react-router';
import { CheckInDialog } from '../components/CheckInDialog';
import { ErrorView, Loading } from '../components/Status';
import { useLeagues, useMe } from '../hooks/queries';
import { t } from '../i18n/t';
import { formatDayMonth } from '../lib/dates';
import { formatPercent } from '../lib/numbers';

/** Screen 3: prominent check-in card + my leagues. */
export function Home() {
  const me = useMe();
  const leagues = useLeagues();
  const [checkingIn, setCheckingIn] = useState(false);
  const last = me.data?.lastCheckIn ?? null;
  const today = me.data?.today;

  return (
    <div className="stack">
      <section className="card checkin">
        <h2>{t('home.checkInTitle')}</h2>
        <p>
          {last === null
            ? t('home.checkInNone')
            : last.forDate === today
              ? t('home.checkInToday', { value: formatPercent(last.ytdPercent) })
              : t('home.checkInLast', { value: formatPercent(last.ytdPercent), date: formatDayMonth(last.forDate) })}
        </p>
        <button className="big" onClick={() => setCheckingIn(true)}>{t('home.checkInButton')}</button>
      </section>
      {checkingIn && (
        <CheckInDialog
          initial={last?.forDate === today ? last?.ytdPercent : undefined}
          onClose={() => setCheckingIn(false)}
        />
      )}

      <section>
        <div className="row">
          <h2>{t('home.leagues')}</h2>
          <Link className="button secondary" to="/ligor/ny">{t('home.createLeague')}</Link>
        </div>
        {leagues.isPending ? (
          <Loading />
        ) : leagues.isError ? (
          <ErrorView error={leagues.error} onRetry={() => void leagues.refetch()} />
        ) : leagues.data.length === 0 ? (
          <p className="muted">{t('home.noLeagues')}</p>
        ) : (
          <ul className="list">
            {leagues.data.map((league) => (
              <li key={league.id}>
                <Link to={`/liga/${league.id}`} className="list-item">
                  <strong>{league.name}</strong>
                  <span className="muted small">
                    {league.myStatus === 'Pending'
                      ? t('home.pending')
                      : t('home.members', { count: league.activeMemberCount, capacity: league.capacity })}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
