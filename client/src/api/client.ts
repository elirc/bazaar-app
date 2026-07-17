const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

/** Error thrown when the API returns a non-2xx response. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, message: string, problem?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export interface RequestOptions {
  method?: string
  body?: unknown
  signal?: AbortSignal
  query?: Record<string, string | number | boolean | undefined | null>
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const url = `${API_BASE}${path}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params.append(key, String(value))
    }
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

/** Typed fetch wrapper. Parses JSON, throws {@link ApiError} on failure. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, signal, query } = options
  const headers: Record<string, string> = {}
  let payload: BodyInit | undefined
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  const response = await fetch(buildUrl(path, query), {
    method,
    headers,
    body: payload,
    signal,
  })

  if (!response.ok) {
    let problem: unknown
    let message = `Request failed with status ${response.status}`
    try {
      problem = await response.json()
      if (problem && typeof problem === 'object' && 'title' in problem) {
        const title = (problem as { title: unknown }).title
        if (typeof title === 'string' && title.length > 0) message = title
      }
    } catch {
      // Non-JSON error body; keep the default message.
    }
    throw new ApiError(response.status, message, problem)
  }

  if (response.status === 204) return undefined as T
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('application/json')) {
    return (await response.text()) as unknown as T
  }
  return (await response.json()) as T
}
