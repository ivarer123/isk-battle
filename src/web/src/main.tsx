import { StrictMode, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { App } from './App';
import { DevAuthProvider } from './auth/DevAuthProvider';
import './styles.css';

async function authProvider(): Promise<(children: ReactNode) => ReactNode> {
  if (import.meta.env.VITE_AUTH_MODE === 'dev') {
    return (children) => <DevAuthProvider>{children}</DevAuthProvider>;
  }
  const [{ PublicClientApplication }, { EntraAuthProvider }] = await Promise.all([
    import('@azure/msal-browser'),
    import('./auth/EntraAuthProvider'),
  ]);
  const instance = new PublicClientApplication({
    auth: {
      clientId: import.meta.env.VITE_ENTRA_CLIENT_ID as string,
      authority: import.meta.env.VITE_ENTRA_AUTHORITY as string,
      redirectUri: window.location.origin,
    },
    cache: { cacheLocation: 'localStorage' },
  });
  await instance.initialize();
  await instance.handleRedirectPromise();
  return (children) => <EntraAuthProvider instance={instance}>{children}</EntraAuthProvider>;
}

const withAuth = await authProvider();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>{withAuth(<App />)}</BrowserRouter>
  </StrictMode>,
);
