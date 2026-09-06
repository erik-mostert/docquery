import { ApiError } from './client'

/** One line a person can act on, from whatever the API client threw. */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const retry = error.retryAfterSeconds ? ` Retry in ${error.retryAfterSeconds} s.` : ''
    return `${error.title}${error.detail ? `: ${error.detail}` : ''}${retry}`
  }
  return error instanceof Error ? error.message : String(error)
}
