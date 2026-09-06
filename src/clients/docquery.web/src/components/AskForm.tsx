import { useId, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { describeError } from '../api/errors'
import type { Answer, DocumentSummary } from '../api/types'

interface Props {
  documents: DocumentSummary[]
  onAnswer: (answer: Answer) => void
}

/** Asks a question over every embedded document, or over the one selected. */
export function AskForm({ documents, onAnswer }: Props) {
  const api = useApi()
  const questionId = useId()
  const scopeId = useId()
  const [question, setQuestion] = useState('')
  const [documentId, setDocumentId] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const askable = documents.filter((document) => document.status === 'Embedded')

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (question.trim().length === 0) {
      setError('Type a question first.')
      return
    }

    setBusy(true)
    setError(null)
    try {
      const answer = await api.askQuestion({ question: question.trim(), documentId: documentId || undefined })
      onAnswer(answer)
    } catch (e) {
      setError(describeError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="ask">
      <label htmlFor={questionId}>Question</label>
      <textarea
        id={questionId}
        rows={3}
        value={question}
        disabled={busy}
        placeholder="What does the document say about…?"
        onChange={(event) => {
          setQuestion(event.target.value)
          setError(null)
        }}
      />
      <label htmlFor={scopeId}>Search in</label>
      <select id={scopeId} value={documentId} disabled={busy} onChange={(event) => setDocumentId(event.target.value)}>
        <option value="">All documents</option>
        {askable.map((document) => (
          <option key={document.id} value={document.id}>
            {document.fileName}
          </option>
        ))}
      </select>
      <button type="submit" disabled={busy}>
        {busy ? 'Thinking…' : 'Ask'}
      </button>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </form>
  )
}
