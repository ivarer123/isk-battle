import { useMemo, useState, type ReactNode } from 'react';
import { AuthContext, type Auth } from './AuthContext';

const STORAGE_KEY = 'isk-battle.devUser';

/** Local development only: the API trusts `X-Dev-User` when it runs with Auth:DevMode in Development. */
export function DevAuthProvider({ children }: { children: ReactNode }) {
  const [name, setName] = useState(() => localStorage.getItem(STORAGE_KEY));

  const auth = useMemo<Auth>(
    () => ({
      mode: 'dev',
      status: name ? 'signedIn' : 'signedOut',
      signIn: async (devName) => {
        const value = devName?.trim();
        if (!value) return;
        localStorage.setItem(STORAGE_KEY, value);
        setName(value);
      },
      signOut: async () => {
        localStorage.removeItem(STORAGE_KEY);
        setName(null);
      },
      getHeaders: async (): Promise<Record<string, string>> => (name ? { 'X-Dev-User': name } : {}),
    }),
    [name],
  );

  return <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>;
}
