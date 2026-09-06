import { describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient } from './client'

function jsonResponse(body: unknown, init: ResponseInit = {}): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
}

function problemResponse(status: number, title: string, detail?: string, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify({ status, title, detail }), {
    status,
    headers: { 'Content-Type': 'application/problem+json', ...headers },
  })
}

function client(fetchMock: typeof fetch) {
  return createApiClient({ commandApiUrl: 'http://cmd/', queryApiUrl: 'http://qry', fetch: fetchMock })
}

describe('uploadDocument', () => {
  it('posts the file as multipart to the command API and returns the document id', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ documentId: 'doc-1' }, { status: 202 }))
    const file = new File(['%PDF-1.4'], 'report.pdf', { type: 'application/pdf' })

    const id = await client(fetchMock).uploadDocument(file)

    expect(id).toBe('doc-1')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('http://cmd/documents')
    expect(init?.method).toBe('POST')
    const body = init?.body as FormData
    expect(body.get('file')).toBeInstanceOf(File)
    expect((body.get('file') as File).name).toBe('report.pdf')
  })

  it('turns problem details into an ApiError', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(problemResponse(413, 'File too large', 'The limit is 1 byte.'))
    const file = new File(['x'], 'big.pdf', { type: 'application/pdf' })

    const error = await client(fetchMock).uploadDocument(file).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(413)
    expect((error as ApiError).title).toBe('File too large')
    expect((error as ApiError).detail).toBe('The limit is 1 byte.')
  })

  it('carries Retry-After on rate limiting', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(problemResponse(429, 'Too many uploads', undefined, { 'Retry-After': '42' }))

    const error = (await client(fetchMock).uploadDocument(new File(['x'], 'a.pdf')).catch((e: unknown) => e)) as ApiError

    expect(error.retryAfterSeconds).toBe(42)
  })
})

describe('listDocuments and getDocument', () => {
  it('reads the list from the query API', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse([{ id: 'a', fileName: 'a.pdf', status: 'Embedded' }]))

    const documents = await client(fetchMock).listDocuments()

    expect(fetchMock.mock.calls[0][0]).toBe('http://qry/documents')
    expect(documents).toHaveLength(1)
    expect(documents[0].fileName).toBe('a.pdf')
  })

  it('returns null when a document does not exist', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 404 }))

    const document = await client(fetchMock).getDocument('missing')

    expect(document).toBeNull()
    expect(fetchMock.mock.calls[0][0]).toBe('http://qry/documents/missing')
  })
})

describe('askQuestion', () => {
  it('posts the question as JSON and returns the answer', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ answer: 'Yes [1]', citations: [] }))

    const answer = await client(fetchMock).askQuestion({ question: 'Is it?', documentId: 'doc-1', topK: 3 })

    expect(answer.answer).toBe('Yes [1]')
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('http://qry/queries')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual({ question: 'Is it?', documentId: 'doc-1', topK: 3 })
  })

  it('falls back to the status text when the error body is not JSON', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('boom', { status: 500, statusText: 'Internal Server Error' }))

    const error = (await client(fetchMock).askQuestion({ question: 'q' }).catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(500)
    expect(error.title).toBe('Internal Server Error')
  })
})
