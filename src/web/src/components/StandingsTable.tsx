import type { StandingsRow } from '../api/client';
import { t } from '../i18n/t';
import { formatPercent } from '../lib/numbers';

export function StandingsTable({ rows }: { rows: StandingsRow[] }) {
  if (rows.length === 0) return <p className="muted">{t('standings.empty')}</p>;
  return (
    <>
      <table className="standings">
        <thead>
          <tr>
            <th scope="col">{t('standings.rank')}</th>
            <th scope="col">{t('standings.name')}</th>
            <th scope="col" className="num">{t('standings.return')}</th>
            <th scope="col" className="num">{t('standings.ytd')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.userId} className={row.isMe ? 'me' : undefined}>
              <td>{row.rank ?? ''}</td>
              <td>{row.displayName}</td>
              <td className="num">
                {row.status === 'Ranked' && row.periodReturn !== null ? (
                  <span className={row.periodReturn < 0 ? 'neg' : row.periodReturn > 0 ? 'pos' : undefined}>
                    {formatPercent(row.periodReturn)}
                  </span>
                ) : (
                  <span className={`badge ${row.status.toLowerCase()}`}>
                    {t(row.status === 'Ny' ? 'standings.status.Ny' : 'standings.status.Pausad')}
                  </span>
                )}
              </td>
              <td className="num muted">{row.ytdPercent !== null ? formatPercent(row.ytdPercent) : '–'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {rows.some((r) => r.status !== 'Ranked') && <p className="muted small">{t('standings.legend')}</p>}
    </>
  );
}
