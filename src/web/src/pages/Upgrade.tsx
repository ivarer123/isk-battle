import { Link, useParams } from 'react-router';
import { ErrorView, Loading } from '../components/Status';
import { useLeague } from '../hooks/queries';
import { t } from '../i18n/t';
import { formatOre } from '../lib/numbers';

/** Screen 7: tier cards. Checkout is wired up with payments (milestone 7). */
export function Upgrade() {
  const { id = '' } = useParams();
  const league = useLeague(id);
  if (league.isPending) return <Loading />;
  if (league.isError) return <ErrorView error={league.error} onRetry={() => void league.refetch()} />;
  const { data } = league;

  return (
    <div className="stack">
      <Link to={`/liga/${data.id}`} className="muted">‹ {data.name}</Link>
      <h1>{t('upgrade.title')}</h1>
      <p>{t('upgrade.current', { capacity: data.capacity })}</p>
      {data.upgradeOptions.length === 0 && <p>{t('upgrade.max')}</p>}
      {data.upgradeOptions.map((option) => (
        <div key={option.tier} className="card tier">
          <strong>{t('upgrade.capacity', { capacity: option.capacity })}</strong>
          <span className="price">{formatOre(option.priceOre)}</span>
        </div>
      ))}
    </div>
  );
}
