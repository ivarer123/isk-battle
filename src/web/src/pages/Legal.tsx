import { t } from '../i18n/t';

/** Screen 10: legal pages. Final texts arrive with milestone 9. */
export function Legal({ page }: { page: 'terms' | 'privacy' }) {
  return (
    <article className="stack">
      <h1>{page === 'terms' ? t('footer.terms') : t('footer.privacy')}</h1>
      <p className="muted">Text kommer före lansering.</p>
    </article>
  );
}
