import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it } from 'vitest'
import { ApiProvider } from '../api/ApiProvider'
import { makeDocument, makeFakeApi } from '../test/fakeApi'
import { useDocuments } from './useDocuments'

// Real timers with a short poll interval: Testing Library's waitFor relies on timers itself.
const pollMs = 30

function wrapperFor(api: ReturnType<typeof makeFakeApi>) {
  return ({ children }: { children: ReactNode }) => <ApiProvider client={api}>{children}</ApiProvider>
}

const settle = () => new Promise((resolve) => setTimeout(resolve, pollMs * 5))

describe('useDocuments', () => {
  it('loads the list once when nothing is processing', async () => {
    const api = makeFakeApi([makeDocument({ status: 'Embedded' })])

    const { result } = renderHook(() => useDocuments(pollMs), { wrapper: wrapperFor(api) })

    await waitFor(() => expect(result.current.loading).toBe(false))
    expect(result.current.documents).toHaveLength(1)
    await act(settle)
    expect(api.listDocuments).toHaveBeenCalledTimes(1)
  })

  it('polls while a document is processing and stops once it settles', async () => {
    const api = makeFakeApi()
    api.listDocuments
      .mockResolvedValueOnce([makeDocument({ status: 'Uploaded', chunkCount: null })])
      .mockResolvedValueOnce([makeDocument({ status: 'Chunked' })])
      .mockResolvedValue([makeDocument({ status: 'Embedded' })])

    const { result } = renderHook(() => useDocuments(pollMs), { wrapper: wrapperFor(api) })

    await waitFor(() => expect(result.current.documents[0]?.status).toBe('Embedded'))
    expect(api.listDocuments.mock.calls.length).toBeGreaterThanOrEqual(3)
    const callsWhenSettled = api.listDocuments.mock.calls.length
    await act(settle)
    expect(api.listDocuments).toHaveBeenCalledTimes(callsWhenSettled)
  })

  it('exposes the error message and lets refresh recover', async () => {
    const api = makeFakeApi()
    api.listDocuments.mockRejectedValueOnce(new Error('query api down')).mockResolvedValue([makeDocument()])

    const { result } = renderHook(() => useDocuments(pollMs), { wrapper: wrapperFor(api) })

    await waitFor(() => expect(result.current.error).toBe('query api down'))
    await act(async () => {
      await result.current.refresh()
    })
    expect(result.current.error).toBeNull()
    expect(result.current.documents).toHaveLength(1)
  })
})
