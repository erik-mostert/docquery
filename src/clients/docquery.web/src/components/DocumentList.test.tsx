import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { makeDocument } from '../test/fakeApi'
import { DocumentList } from './DocumentList'

describe('DocumentList', () => {
  it('shows an empty state', () => {
    render(<DocumentList documents={[]} loading={false} error={null} />)

    expect(screen.getByText(/no documents yet/i)).toBeInTheDocument()
  })

  it('shows the loading state before the first load', () => {
    render(<DocumentList documents={[]} loading={true} error={null} />)

    expect(screen.getByText(/loading documents/i)).toBeInTheDocument()
  })

  it('shows the error when loading failed', () => {
    render(<DocumentList documents={[]} loading={false} error="query api down" />)

    expect(screen.getByRole('alert')).toHaveTextContent('query api down')
  })

  it('lists each document with its status, chunk count and failure reason', () => {
    render(
      <DocumentList
        loading={false}
        error={null}
        documents={[
          makeDocument({ id: 'a', fileName: 'a.pdf', status: 'Embedded', chunkCount: 12 }),
          makeDocument({ id: 'b', fileName: 'b.pdf', status: 'Uploaded', chunkCount: null }),
          makeDocument({ id: 'c', fileName: 'c.pdf', status: 'Failed', chunkCount: null, failureReason: 'No text found.' }),
        ]}
      />,
    )

    const rows = screen.getAllByRole('row').slice(1)
    expect(rows).toHaveLength(3)
    expect(rows[0]).toHaveTextContent('a.pdf')
    expect(rows[0]).toHaveTextContent('Embedded')
    expect(rows[0]).toHaveTextContent('12')
    expect(rows[1]).toHaveTextContent('Uploaded')
    expect(rows[2]).toHaveTextContent('Failed')
    expect(screen.getByRole('note')).toHaveTextContent('No text found.')
  })
})
