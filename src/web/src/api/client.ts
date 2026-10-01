import createClient, { type Middleware } from 'openapi-fetch';
import type { components, paths } from './schema';

export type Schemas = components['schemas'];
export type Me = Schemas['MeResponse'];
export type CheckIn = Schemas['CheckInResponse'];
export type LeagueSummary = Schemas['LeagueSummary'];
export type LeagueDetail = Schemas['LeagueDetail'];
export type Standings = Schemas['StandingsResponse'];
export type StandingsRow = Schemas['StandingsRowResponse'];
// Enums appear in nullable properties, so the generated enum types include null.
export type Cadence = NonNullable<Schemas['Cadence']>;
export type PeriodType = NonNullable<Schemas['PeriodType']>;
export type LeagueTier = NonNullable<Schemas['LeagueTier']>;

/** An API error carrying the ProblemDetails `code` and any extensions. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string | undefined,
    readonly body: Record<string, unknown>,
  ) {
    super(`API ${status}${code ? ` ${code}` : ''}`);
  }
}

export type HeaderSource = () => Promise<Record<string, string>>;

export function createApi(getHeaders: HeaderSource) {
  const auth: Middleware = {
    async onRequest({ request }) {
      for (const [name, value] of Object.entries(await getHeaders())) request.headers.set(name, value);
      return request;
    },
  };
  const api = createClient<paths>({ baseUrl: import.meta.env.VITE_API_BASE_URL ?? '/api' });
  api.use(auth);
  return api;
}

export type Api = ReturnType<typeof createApi>;

/** Unwraps an openapi-fetch result, throwing ApiError on failure. */
export async function unwrap<T>(
  promise: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await promise;
  if (!response.ok) {
    const body = (error ?? {}) as Record<string, unknown>;
    throw new ApiError(response.status, typeof body.code === 'string' ? body.code : undefined, body);
  }
  return data as T;
}
