import { useMemo, useState } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiContext } from './api/ApiContext';
import { ApiError, createApi } from './api/client';
import { useAuth } from './auth/AuthContext';
import { useMe } from './hooks/queries';
import { Layout } from './components/Layout';
import { Loading, ErrorView } from './components/Status';
import { Landing } from './pages/Landing';
import { Onboarding } from './pages/Onboarding';
import { Home } from './pages/Home';
import { CreateLeague } from './pages/CreateLeague';
import { LeagueView } from './pages/LeagueView';
import { LeagueSettings } from './pages/LeagueSettings';
import { Upgrade } from './pages/Upgrade';
import { Join } from './pages/Join';
import { Profile } from './pages/Profile';
import { Legal } from './pages/Legal';

export function App() {
  const auth = useAuth();
  const api = useMemo(() => createApi(auth.getHeaders), [auth.getHeaders]);
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // One retry covers the SQL auto-pause wake-up (ARCHITECTURE.md §10); 4xx are final.
            retry: (failureCount, error) => failureCount < 1 && !(error instanceof ApiError && error.status < 500),
            retryDelay: 2000,
            staleTime: 30_000,
          },
        },
      }),
  );

  return (
    <ApiContext.Provider value={api}>
      <QueryClientProvider client={queryClient}>
        <Routes>
          <Route path="/villkor" element={<Layout><Legal page="terms" /></Layout>} />
          <Route path="/integritet" element={<Layout><Legal page="privacy" /></Layout>} />
          <Route path="*" element={<SignedInRoutes />} />
        </Routes>
      </QueryClientProvider>
    </ApiContext.Provider>
  );
}

function SignedInRoutes() {
  const auth = useAuth();
  const location = useLocation();
  const me = useMe(auth.status === 'signedIn');

  if (auth.status === 'loading') return <Loading />;
  if (auth.status === 'signedOut') return <Landing returnTo={location.pathname} />;
  if (me.isPending) return <Loading />;
  if (me.isError) return <ErrorView onRetry={() => void me.refetch()} />;
  if (!me.data.displayName) return <Onboarding />;

  return (
    <Layout signedIn>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/ligor/ny" element={<CreateLeague />} />
        <Route path="/liga/:id" element={<LeagueView />} />
        <Route path="/liga/:id/installningar" element={<LeagueSettings />} />
        <Route path="/liga/:id/uppgradera" element={<Upgrade />} />
        <Route path="/ga-med/:code" element={<Join />} />
        <Route path="/profil" element={<Profile />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Layout>
  );
}
