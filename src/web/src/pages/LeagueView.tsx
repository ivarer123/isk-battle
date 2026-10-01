import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import type { LeagueDetail, PeriodType, Standings } from '../api/client';
import { StandingsTable } from '../components/StandingsTable';
import { ErrorView, Loading } from '../components/Status';
import { useLeague, useRefreshAfterPayment, useStandings } from '../hooks/queries';
import { t } from '../i18n/t';
import { addDays, formatDayMonth, formatMonthYear } from '../lib/dates';

/** Screen 5: period tabs + standings. */
export function LeagueView() {
  const { id = '' } = useParams();
  const [search] = useSearchParams();
  const returnedFromPayment = search.get('betalning') === 'klar';
  const league = useLeague(id);
  useRefreshAfterPayment(id, returnedFromPayment);

  if (league.isPending) return <Loading />;
  if (league.isError) return <ErrorView error={league.error} onRetry={() => void league.refetch()} />;
  const banner = returnedFromPayment && <p className="card checkin">{t('upgrade.success')}</p>;
  if (league.data.myStatus === 'Pending') return <>{banner}<PendingView league={league.data} /></>;
  return <>{banner}<ActiveLeague league={league.data} /></>;
}

function PendingView({ league }: { league: LeagueDetail }) {
  return (
    <div className="stack">
      <h1>{league.name}</h1>
      <div className="card">
        <h2>{t('league.pendingTitle')}</h2>
        <p>{t('league.pendingText')}</p>
        {league.upgradeOptions.length > 0 && (
          <>
            <p>{t('league.pendingUpgrade')}</p>
            <Link className="button" to={`/liga/${league.id}/uppgradera`}>{t('settings.upgrade')}</Link>
          </>
        )}
      </div>
    </div>
  );
}

function ActiveLeague({ league }: { league: LeagueDetail }) {
  const [period, setPeriod] = useState<PeriodType>(league.periods[0] ?? 'Year');
  const [date, setDate] = useState<string | undefined>(undefined);
  const standings = useStandings(league.id, period, date);
  const isHost = league.myRole !== 'Member';

  return (
    <div className="stack">
      <div className="row">
        <h1>{league.name}</h1>
        <Link className="button secondary" to={`/liga/${league.id}/installningar`}>
          {isHost ? t('league.invite') : t('league.settings')}
        </Link>
      </div>
      <div className="tabs" role="tablist">
        {league.periods.map((p) => (
          <button
            key={p}
            role="tab"
            aria-selected={p === period}
            className={p === period ? 'tab active' : 'tab'}
            onClick={() => {
              setPeriod(p);
              setDate(undefined);
            }}
          >
            {t(`period.${p}`)}
          </button>
        ))}
      </div>
      {standings.isPending ? (
        <Loading />
      ) : standings.isError ? (
        <ErrorView error={standings.error} onRetry={() => void standings.refetch()} />
      ) : (
        <>
          <PeriodNav standings={standings.data} onChange={setDate} />
          <StandingsTable rows={standings.data.rows} />
        </>
      )}
    </div>
  );
}

function PeriodNav({ standings, onChange }: { standings: Standings; onChange: (date: string) => void }) {
  const { period } = standings;
  const lastDay = addDays(period.end, -1);
  const label =
    period.type === 'Day'
      ? formatDayMonth(period.start) + (lastDay !== period.start ? `–${formatDayMonth(lastDay)}` : '')
      : period.type === 'Week'
        ? t('period.weekLabel', { week: period.isoWeek ?? '', year: period.season })
        : period.type === 'Month'
          ? formatMonthYear(period.start)
          : String(period.season);
  const today = new Date().toISOString().slice(0, 10);

  return (
    <div className="period-nav">
      <button className="secondary" aria-label={t('period.previous')} onClick={() => onChange(addDays(period.start, -1))}>‹</button>
      <span>{label}</span>
      <button
        className="secondary"
        aria-label={t('period.next')}
        disabled={period.end > today}
        onClick={() => onChange(period.end)}
      >
        ›
      </button>
    </div>
  );
}
