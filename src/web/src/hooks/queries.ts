import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useApi } from '../api/ApiContext';
import { ApiError, unwrap, type Cadence, type CheckIn, type LeagueTier, type PeriodType } from '../api/client';

/** One hook per resource (ARCHITECTURE.md §9). Components never call fetch directly. */
export const keys = {
  me: ['me'] as const,
  leagues: ['leagues'] as const,
  league: (id: string) => ['leagues', id] as const,
  standings: (id: string, period: PeriodType, date: string | undefined) => ['leagues', id, 'standings', period, date] as const,
};

export function useMe(enabled = true) {
  const api = useApi();
  return useQuery({ queryKey: keys.me, queryFn: () => unwrap(api.GET('/me')), enabled });
}

export function useUpdateMe() {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { displayName?: string; emailRemindersEnabled?: boolean }) =>
      unwrap(api.PATCH('/me', { body: { displayName: body.displayName ?? null, emailRemindersEnabled: body.emailRemindersEnabled ?? null } })),
    onSuccess: (me) => queryClient.setQueryData(keys.me, me),
  });
}

export function useDeleteMe() {
  const api = useApi();
  return useMutation({ mutationFn: () => unwrap(api.DELETE('/me')) });
}

export type CheckInResult =
  | { kind: 'saved'; checkIn: CheckIn }
  | { kind: 'confirm'; previousYtdPercent: number };

export function useCheckIn() {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ ytdPercent, confirmed }: { ytdPercent: number; confirmed: boolean }): Promise<CheckInResult> => {
      try {
        return { kind: 'saved', checkIn: await unwrap(api.POST('/check-ins', { body: { ytdPercent, confirmed } })) };
      } catch (error) {
        if (error instanceof ApiError && error.status === 422 && error.body.requiresConfirmation) {
          return { kind: 'confirm', previousYtdPercent: Number(error.body.previousYtdPercent) };
        }
        throw error;
      }
    },
    onSuccess: (result) => {
      if (result.kind === 'saved') void queryClient.invalidateQueries();
    },
  });
}

export function useLeagues() {
  const api = useApi();
  return useQuery({ queryKey: keys.leagues, queryFn: () => unwrap(api.GET('/leagues')) });
}

export function useLeague(id: string) {
  const api = useApi();
  return useQuery({
    queryKey: keys.league(id),
    queryFn: () => unwrap(api.GET('/leagues/{id}', { params: { path: { id } } })),
  });
}

export function useCreateLeague() {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; cadence: Cadence }) => unwrap(api.POST('/leagues', { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.leagues }),
  });
}

export function useUpdateLeague(id: string) {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name?: string; nextSeasonCadence?: Cadence }) =>
      unwrap(api.PATCH('/leagues/{id}', {
        params: { path: { id } },
        body: { name: body.name ?? null, nextSeasonCadence: body.nextSeasonCadence ?? null },
      })),
    onSuccess: (league) => {
      queryClient.setQueryData(keys.league(id), league);
      void queryClient.invalidateQueries({ queryKey: keys.leagues });
    },
  });
}

export function useRegenerateInvite(id: string) {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST('/leagues/{id}/invite-code', { params: { path: { id } } })),
    onSuccess: (league) => queryClient.setQueryData(keys.league(id), league),
  });
}

export function useDeleteLeague(id: string) {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE('/leagues/{id}', { params: { path: { id } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.leagues }),
  });
}

export function useRemoveMember(id: string) {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) =>
      unwrap(api.DELETE('/leagues/{id}/members/{userId}', { params: { path: { id, userId } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.leagues }),
  });
}

export function useJoinLeague() {
  const api = useApi();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (inviteCode: string) => unwrap(api.POST('/leagues/join', { body: { inviteCode } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.leagues }),
  });
}

export function useStandings(id: string, period: PeriodType, date: string | undefined, enabled = true) {
  const api = useApi();
  return useQuery({
    queryKey: keys.standings(id, period, date),
    queryFn: () =>
      unwrap(api.GET('/leagues/{id}/standings', {
        params: { path: { id }, query: { period: period.toLowerCase(), date } },
      })),
    enabled,
    placeholderData: (previous) => previous,
  });
}

export type { LeagueTier };
