import { useState } from 'react';
import { Link } from 'react-router';
import { CheckInDialog } from '../components/CheckInDialog';
import { ErrorView, Loading } from '../components/Status';
import { useLeagues, useMe } from '../hooks/queries';
import { t } from '../i18n/t';
import { formatDayMonth, formatStockholmDateTime, formatWeekdayDayMonth } from '../lib/dates';
import { formatPercent } from '../lib/numbers';

/** Screen 3: prominent check-in card + my leagues. */
export function Home() {
  const me = useMe();
  const leagues = useLeagues();
  const [checkingIn, setCheckingIn] = useState(false);
  const last = me.data?.lastCheckIn ?? null;
  const openWindow = me.data?.openWindow ?? null;
  // A check-in for the open window's trading day can still be changed until the window closes.
  const current = openWindow !== null && last?.forDate === openWindow.tradingDay ? last : null;

  return (
    <div className="stack">
      <section className="card checkin">
        <h2>{t('home.checkInTitle')}</h2>
        {openWindow === null ? (
          me.data && <p>{t('home.checkInClosed', { opens: formatStockholmDateTime(me.data.nextWindowOpensAt) })}</p>
        ) : current !== null ? (
          <p>
            {t('home.checkInDone', {
              day: formatWeekdayDayMonth(openWindow.tradingDay),
              value: formatPercent(current.ytdPercent),
              closes: formatStockholmDateTime(openWindow.closesAt),
            })}
          </p>
        ) : (
          <p>{t('home.checkInOpen', { day: formatWeekdayDayMonth(openWindow.tradingDay), closes: formatStockholmDateTime(openWindow.closesAt) })}</p>
        )}
        {current === null && (
          <p className="muted small">
            {last === null
              ? t('home.checkInNone')
              : t('home.checkInLast', { value: formatPercent(last.ytdPercent), date: formatDayMonth(last.forDate) })}
          </p>
        )}
        <button className="big" disabled={openWindow === null} onClick={() => setCheckingIn(true)}>{t('home.checkInButton')}</button>
        <p className="muted small">{t('home.checkInWhy')}</p>
      </section>
      {checkingIn && <CheckInDialog initial={current?.ytdPercent} onClose={() => setCheckingIn(false)} />}

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
