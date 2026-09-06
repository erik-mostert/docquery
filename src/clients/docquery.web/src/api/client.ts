import type { Answer, AskRequest, DocumentSummary } from './types'

/** RFC 9457 problem details as both APIs return them. */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail?: string
  readonly retryAfterSeconds?: number

  constructor(status: number, title: string, detail?: string, retryAfterSeconds?: number) {
    super(detail ? `${title}: ${detail}` : title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
    this.retryAfterSeconds = retryAfterSeconds
  }
}

export interface ApiClient {
  uploadDocument(file: File): Promise<string>
  listDocuments(): Promise<DocumentSummary[]>
  getDocument(id: string): Promise<DocumentSummary | null>
  askQuestion(request: AskRequest): Promise<Answer>
}

export interface ApiClientOptions {
  commandApiUrl: string
  queryApiUrl: string
  fetch?: typeof fetch
}

export function createApiClient(options: ApiClientOptions): ApiClient {
  const doFetch = options.fetch ?? ((input, init) => fetch(input, init))
  const commandApi = trimSlash(options.commandApiUrl)
  const queryApi = trimSlash(options.queryApiUrl)

  return {
    async uploadDocument(file) {
      const form = new FormData()
      form.append('file', file, file.name)
      const response = await doFetch(`${commandApi}/documents`, { method: 'POST', body: form })
      const body = await readJson<{ documentId: string }>(response)
      return body.documentId
    },

    async listDocuments() {
      const response = await doFetch(`${queryApi}/documents`)
      return readJson<DocumentSummary[]>(response)
    },

    async getDocument(id) {
      const response = await doFetch(`${queryApi}/documents/${encodeURIComponent(id)}`)
      if (response.status === 404) {
        return null
      }
      return readJson<DocumentSummary>(response)
    },

    async askQuestion(request) {
      const response = await doFetch(`${queryApi}/queries`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      })
      return readJson<Answer>(response)
    },
  }
}

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw await toApiError(response)
  }
  return (await response.json()) as T
}

async function toApiError(response: Response): Promise<ApiError> {
  const retryAfter = response.headers.get('Retry-After')
  const retryAfterSeconds = retryAfter ? Number(retryAfter) : undefined
  try {
    const problem = (await response.json()) as { title?: string; detail?: string }
    return new ApiError(response.status, problem.title ?? response.statusText, problem.detail, retryAfterSeconds)
  } catch {
    return new ApiError(response.status, response.statusText || `HTTP ${response.status}`, undefined, retryAfterSeconds)
  }
}

function trimSlash(url: string): string {
  return url.endsWith('/') ? url.slice(0, -1) : url
}

/** The client the app uses; URLs come from the AppHost (VITE_API_URL, VITE_QUERY_API_URL) or the launch profiles. */
export const api = createApiClient({
  commandApiUrl: import.meta.env.VITE_API_URL ?? 'http://localhost:5290',
  queryApiUrl: import.meta.env.VITE_QUERY_API_URL ?? 'http://localhost:5291',
})
