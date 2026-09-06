import { useCallback, useEffect, useRef, useState } from 'react'
import { useApi } from '../api/useApi'
import { isProcessing, type DocumentSummary } from '../api/types'

export interface DocumentsState {
  documents: DocumentSummary[]
  loading: boolean
  error: string | null
  refresh: () => Promise<void>
}

/**
 * Loads the document list and polls it while any document is still being processed, so uploads visibly move
 * from Uploaded to Chunked to Embedded without a refresh. Polling stops when every document is settled.
 */
export function useDocuments(pollIntervalMs = 3000): DocumentsState {
  const api = useApi()
  const [documents, setDocuments] = useState<DocumentSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const mounted = useRef(true)

  const refresh = useCallback(async () => {
    try {
      const list = await api.listDocuments()
      if (mounted.current) {
        setDocuments(list)
        setError(null)
      }
    } catch (e) {
      if (mounted.current) {
        setError(e instanceof Error ? e.message : String(e))
      }
    } finally {
      if (mounted.current) {
        setLoading(false)
      }
    }
  }, [api])

  // Loading from the API on mount is synchronising with an external system, which is what effects are for.
  /* oxlint-disable react/set-state-in-effect */
  useEffect(() => {
    mounted.current = true
    void refresh()
    return () => {
      mounted.current = false
    }
  }, [refresh])
  /* oxlint-enable react/set-state-in-effect */

  const anyProcessing = documents.some(isProcessing)

  useEffect(() => {
    if (!anyProcessing) {
      return
    }
    const timer = setInterval(() => {
      void refresh()
    }, pollIntervalMs)
    return () => clearInterval(timer)
  }, [anyProcessing, pollIntervalMs, refresh])

  return { documents, loading, error, refresh }
}
