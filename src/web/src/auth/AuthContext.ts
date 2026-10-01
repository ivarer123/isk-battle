import { createContext, useContext } from 'react';

export type AuthStatus = 'loading' | 'signedOut' | 'signedIn';

export interface Auth {
  status: AuthStatus;
  /** Dev mode takes a player name; Entra mode redirects to the hosted sign-in. */
  signIn(devName?: string): Promise<void>;
  signOut(): Promise<void>;
  /** Headers for API calls, refreshing tokens as needed. */
  getHeaders(): Promise<Record<string, string>>;
  mode: 'dev' | 'entra';
}

export const AuthContext = createContext<Auth | null>(null);

export function useAuth(): Auth {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error('useAuth must be used inside an AuthProvider');
  return auth;
}
