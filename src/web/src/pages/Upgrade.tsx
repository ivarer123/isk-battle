import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import type { LeagueTier } from '../api/client';
import { ErrorView, FormError, Loading } from '../components/Status';
import { useCheckout, useLeague } from '../hooks/queries';
import { t } from '../i18n/t';
import { formatOre } from '../lib/numbers';

/** Screen 7: tier cards, ångerrätt consent (GAME-DESIGN.md §8), pay via Stripe Checkout. */
export function Upgrade() {
  const { id = '' } = useParams();
  const [search] = useSearchParams();
  const league = useLeague(id);
  const checkout = useCheckout(id);
  const [tier, setTier] = useState<LeagueTier | null>(null);
  const [consent, setConsent] = useState(false);

  if (league.isPending) return <Loading />;
  if (league.isError) return <ErrorView error={league.error} onRetry={() => void league.refetch()} />;
  const { data } = league;
  const options = data.upgradeOptions.filter((o): o is typeof o & { tier: LeagueTier } => o.tier !== null);
  const selected = options.find((o) => o.tier === (tier ?? options[0]?.tier));

  async function pay() {
    if (!selected) return;
    const result = await checkout.mutateAsync(selected.tier).catch(() => null);
    if (result) window.location.assign(result.checkoutUrl);
  }

  return (
    <div className="stack">
      <Link to={`/liga/${data.id}`} className="muted">‹ {data.name}</Link>
      <h1>{t('upgrade.title')}</h1>
      {search.get('betalning') === 'avbruten' && <p className="card">{t('upgrade.cancelled')}</p>}
      <p>{t('upgrade.current', { capacity: data.capacity })}</p>
      {options.length === 0 ? (
        <p>{t('upgrade.max')}</p>
      ) : (
        <>
          <fieldset className="stack">
            {options.map((option) => (
              <label key={option.tier} className={`card tier selectable${option === selected ? ' selected' : ''}`}>
                <input
                  type="radio"
                  name="tier"
                  checked={option === selected}
                  onChange={() => setTier(option.tier)}
                />
                <strong>{t('upgrade.capacity', { capacity: option.capacity })}</strong>
                <span className="price">{formatOre(option.priceOre)}</span>
              </label>
            ))}
          </fieldset>
          <p className="muted small">{t('upgrade.permanent')}</p>
          <label className="checkbox">
            <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} />
            <span>{t('upgrade.consent')}</span>
          </label>
          <FormError error={checkout.error} />
          <button className="big" disabled={!consent || !selected || checkout.isPending} onClick={() => void pay()}>
            {selected ? t('upgrade.pay', { price: formatOre(selected.priceOre) }) : ''}
          </button>
          {data.tier !== 'Free' && <p className="muted small">{t('settings.deletePaidWarning')}</p>}
        </>
      )}
    </div>
  );
}
