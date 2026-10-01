import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import type { Cadence, LeagueDetail } from '../api/client';
import { ErrorView, FormError, Loading } from '../components/Status';
import { useDeleteLeague, useLeague, useMe, useRegenerateInvite, useRemoveMember, useUpdateLeague } from '../hooks/queries';
import { t } from '../i18n/t';

const cadences: Cadence[] = ['Daily', 'Weekly', 'Monthly'];

/** Screen 6: settings for hosts; members only see the member list and "leave". */
export function LeagueSettings() {
  const { id = '' } = useParams();
  const league = useLeague(id);
  if (league.isPending) return <Loading />;
  if (league.isError) return <ErrorView error={league.error} onRetry={() => void league.refetch()} />;
  return <Settings league={league.data} />;
}

function Settings({ league }: { league: LeagueDetail }) {
  const me = useMe();
  const navigate = useNavigate();
  const isHost = league.myRole !== 'Member' && league.myStatus === 'Active';
  const update = useUpdateLeague(league.id);
  const remove = useRemoveMember(league.id);
  const regenerate = useRegenerateInvite(league.id);
  const deleteLeague = useDeleteLeague(league.id);
  const [name, setName] = useState(league.name);
  const [copied, setCopied] = useState(false);
  const inviteUrl = league.inviteCode ? `${window.location.origin}/ga-med/${league.inviteCode}` : null;
  const active = league.members.filter((m) => m.status === 'Active');
  const pending = league.members.filter((m) => m.status === 'Pending');

  async function copy() {
    if (!inviteUrl) return;
    await navigator.clipboard.writeText(inviteUrl);
    setCopied(true);
  }

  async function share() {
    if (inviteUrl) await navigator.share({ title: league.name, url: inviteUrl }).catch(() => undefined);
  }

  async function removeMember(userId: string, displayName: string, self: boolean) {
    if (!window.confirm(self ? t('settings.leaveConfirm') : t('settings.removeConfirm', { name: displayName }))) return;
    await remove.mutateAsync(userId).catch(() => undefined);
    if (self) navigate('/');
  }

  async function onDelete() {
    const warning = league.tier !== 'Free' ? `\n\n${t('settings.deletePaidWarning')}` : '';
    if (!window.confirm(t('settings.deleteConfirm') + warning)) return;
    await deleteLeague.mutateAsync().catch(() => undefined);
    navigate('/');
  }

  return (
    <div className="stack">
      <Link to={`/liga/${league.id}`} className="muted">‹ {league.name}</Link>
      <h1>{t('settings.title')}</h1>

      {isHost && inviteUrl && (
        <section className="card">
          <h2>{t('settings.inviteLink')}</h2>
          <p className="mono">{inviteUrl}</p>
          <div className="actions start">
            <button onClick={() => void copy()}>{copied ? t('settings.copied') : t('settings.copy')}</button>
            {'share' in navigator && <button className="secondary" onClick={() => void share()}>{t('settings.share')}</button>}
            <button
              className="secondary"
              onClick={() => window.confirm(t('settings.regenerateConfirm')) && regenerate.mutate()}
            >
              {t('settings.regenerate')}
            </button>
          </div>
        </section>
      )}

      {isHost && (
        <section className="card stack">
          <div>
            <label htmlFor="league-name">{t('settings.name')}</label>
            <div className="inline">
              <input id="league-name" maxLength={60} value={name} onChange={(e) => setName(e.target.value)} />
              <button disabled={!name.trim() || name === league.name} onClick={() => update.mutate({ name })}>
                {t('common.save')}
              </button>
            </div>
          </div>
          <div>
            <label htmlFor="cadence">{t('settings.nextCadence')}</label>
            <select
              id="cadence"
              value={league.nextSeasonCadence ?? league.cadence ?? undefined}
              onChange={(e) => update.mutate({ nextSeasonCadence: e.target.value as Cadence })}
            >
              {cadences.map((c) => (
                <option key={c} value={c}>{t(`cadence.${c}`)}</option>
              ))}
            </select>
            {league.nextSeasonCadence && (
              <p className="muted small">{t('settings.nextCadenceQueued', { cadence: t(`cadence.${league.nextSeasonCadence}`) })}</p>
            )}
          </div>
          <FormError error={update.error} />
        </section>
      )}

      {isHost && league.upgradeOptions.length > 0 && (
        <Link className="button" to={`/liga/${league.id}/uppgradera`}>{t('settings.upgrade')}</Link>
      )}

      <section>
        <h2>{t('settings.members', { count: league.activeMemberCount, capacity: league.capacity })}</h2>
        <ul className="list">
          {active.map((m) => (
            <li key={m.userId} className="list-item">
              <span>
                {m.displayName} <span className="muted small">{t(`role.${m.role}`)}</span>
              </span>
              {isHost && m.role === 'Member' && (
                <button className="link" onClick={() => void removeMember(m.userId, m.displayName, false)}>
                  {t('settings.remove')}
                </button>
              )}
            </li>
          ))}
        </ul>
        {isHost && pending.length > 0 && (
          <>
            <h3>{t('settings.pendingMembers', { count: pending.length })}</h3>
            <ul className="list">
              {pending.map((m) => (
                <li key={m.userId} className="list-item">
                  <span>{m.displayName}</span>
                  <button className="link" onClick={() => void removeMember(m.userId, m.displayName, false)}>
                    {t('settings.remove')}
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
        <FormError error={remove.error} />
      </section>

      <section className="danger-zone">
        {me.data && (
          <button className="secondary" onClick={() => void removeMember(me.data.id, me.data.displayName, true)}>
            {t('settings.leave')}
          </button>
        )}
        {league.myRole === 'Owner' && (
          <button className="danger" onClick={() => void onDelete()}>{t('settings.delete')}</button>
        )}
        <FormError error={deleteLeague.error} />
      </section>
    </div>
  );
}
