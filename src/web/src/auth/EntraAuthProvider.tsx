import { useMemo, type ReactNode } from 'react';
import {
  InteractionRequiredAuthError,
  InteractionStatus,
  type PublicClientApplication,
} from '@azure/msal-browser';
import { MsalProvider, useMsal } from '@azure/msal-react';
import { AuthContext, type Auth } from './AuthContext';

export const apiScopes = [import.meta.env.VITE_API_SCOPE as string];

/** Microsoft Entra External ID with email one-time passcode (ARCHITECTURE.md §2). */
export function EntraAuthProvider({ instance, children }: { instance: PublicClientApplication; children: ReactNode }) {
  return (
    <MsalProvider instance={instance}>
      <EntraAuth>{children}</EntraAuth>
    </MsalProvider>
  );
}

function EntraAuth({ children }: { children: ReactNode }) {
  const { instance, accounts, inProgress } = useMsal();
  const account = accounts[0];

  const auth = useMemo<Auth>(
    () => ({
      mode: 'entra',
      status: inProgress !== InteractionStatus.None && !account ? 'loading' : account ? 'signedIn' : 'signedOut',
      signIn: async () => {
        await instance.loginRedirect({ scopes: apiScopes, redirectStartPage: window.location.href });
      },
      signOut: async () => {
        await instance.logoutRedirect({ account, postLogoutRedirectUri: '/' });
      },
      getHeaders: async (): Promise<Record<string, string>> => {
        if (!account) return {};
        try {
          const result = await instance.acquireTokenSilent({ scopes: apiScopes, account });
          return { Authorization: `Bearer ${result.accessToken}` };
        } catch (error) {
          if (error instanceof InteractionRequiredAuthError) {
            await instance.acquireTokenRedirect({ scopes: apiScopes, account });
          }
          throw error;
        }
      },
    }),
    [instance, account, inProgress],
  );

  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}
