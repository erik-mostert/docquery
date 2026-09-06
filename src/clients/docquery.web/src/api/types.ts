export type DocumentStatus = 'Uploaded' | 'Chunked' | 'Embedded' | 'Failed'

export interface DocumentSummary {
  id: string
  fileName: string
  sizeInBytes: number
  status: DocumentStatus
  uploadedAt: string
  chunkCount: number | null
  chunkedAt: string | null
  embeddedAt: string | null
  failureReason: string | null
}

export interface Citation {
  number: number
  documentId: string
  fileName: string
  pageNumber: number
  position: number
  excerpt: string
  score: number
}

export interface Answer {
  answer: string
  citations: Citation[]
}

export interface AskRequest {
  question: string
  documentId?: string
  topK?: number
}

/** A document is still moving through the pipeline until it is embedded or has failed. */
export function isProcessing(document: DocumentSummary): boolean {
  return document.status === 'Uploaded' || document.status === 'Chunked'
}
