import { vi } from 'vitest'
import type { ApiClient } from '../api/client'
import type { Answer, DocumentSummary } from '../api/types'

export function makeDocument(overrides: Partial<DocumentSummary> = {}): DocumentSummary {
  return {
    id: 'doc-1',
    fileName: 'report.pdf',
    sizeInBytes: 1234,
    status: 'Embedded',
    uploadedAt: '2026-09-06T09:00:00Z',
    chunkCount: 12,
    chunkedAt: '2026-09-06T09:00:10Z',
    embeddedAt: '2026-09-06T09:00:20Z',
    failureReason: null,
    ...overrides,
  }
}

export function makeAnswer(overrides: Partial<Answer> = {}): Answer {
  return {
    answer: 'Refunds take 14 days [1].',
    citations: [
      {
        number: 1,
        documentId: 'doc-1',
        fileName: 'report.pdf',
        pageNumber: 3,
        position: 7,
        excerpt: 'Refunds are issued within 14 days.',
        score: 0.91,
      },
    ],
    ...overrides,
  }
}

export type FakeApi = { [K in keyof ApiClient]: ReturnType<typeof vi.fn<ApiClient[K]>> }

export function makeFakeApi(documents: DocumentSummary[] = []): FakeApi {
  return {
    uploadDocument: vi.fn<ApiClient['uploadDocument']>().mockResolvedValue('doc-new'),
    listDocuments: vi.fn<ApiClient['listDocuments']>().mockResolvedValue(documents),
    getDocument: vi.fn<ApiClient['getDocument']>().mockResolvedValue(null),
    askQuestion: vi.fn<ApiClient['askQuestion']>().mockResolvedValue(makeAnswer()),
  }
}
