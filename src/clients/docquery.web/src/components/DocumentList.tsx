import type { DocumentSummary } from '../api/types'

interface Props {
  documents: DocumentSummary[]
  loading: boolean
  error: string | null
}

/** The uploaded documents and where each one is in the pipeline. */
export function DocumentList({ documents, loading, error }: Props) {
  if (error) {
    return (
      <p role="alert" className="error">
        Could not load documents: {error}
      </p>
    )
  }

  if (loading) {
    return <p className="muted">Loading documents…</p>
  }

  if (documents.length === 0) {
    return <p className="muted">No documents yet. Upload a PDF to get started.</p>
  }

  return (
    <table className="documents">
      <thead>
        <tr>
          <th>File</th>
          <th>Status</th>
          <th>Chunks</th>
          <th>Uploaded</th>
        </tr>
      </thead>
      <tbody>
        {documents.map((document) => (
          <tr key={document.id}>
            <td>
              <span className="file-name">{document.fileName}</span>
              <span className="muted"> {formatSize(document.sizeInBytes)}</span>
              {document.failureReason && (
                <div className="error" role="note">
                  {document.failureReason}
                </div>
              )}
            </td>
            <td>
              <span className={`status status-${document.status.toLowerCase()}`}>{document.status}</span>
            </td>
            <td>{document.chunkCount ?? '–'}</td>
            <td>
              <time dateTime={document.uploadedAt}>{formatTime(document.uploadedAt)}</time>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleString()
}
