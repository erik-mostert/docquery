import { useId, useRef, useState, type FormEvent } from 'react'
import { useApi } from '../api/useApi'
import { describeError } from '../api/errors'

interface Props {
  onUploaded: (documentId: string) => void
}

/** Picks one PDF and sends it to the command API; the list picks up the new document through polling. */
export function UploadForm({ onUploaded }: Props) {
  const api = useApi()
  const inputId = useId()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!file) {
      setError('Choose a PDF first.')
      return
    }

    setBusy(true)
    setError(null)
    try {
      const id = await api.uploadDocument(file)
      setFile(null)
      if (input.current) {
        input.current.value = ''
      }
      onUploaded(id)
    } catch (e) {
      setError(describeError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="upload">
      <label htmlFor={inputId}>PDF file</label>
      <input
        id={inputId}
        ref={input}
        type="file"
        accept="application/pdf,.pdf"
        disabled={busy}
        onChange={(event) => {
          setFile(event.target.files?.[0] ?? null)
          setError(null)
        }}
      />
      <button type="submit" disabled={busy}>
        {busy ? 'Uploading…' : 'Upload'}
      </button>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </form>
  )
}
