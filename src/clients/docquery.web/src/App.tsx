import { useState } from 'react'
import type { Answer as AnswerModel } from './api/types'
import { Answer } from './components/Answer'
import { AskForm } from './components/AskForm'
import { DocumentList } from './components/DocumentList'
import { UploadForm } from './components/UploadForm'
import { useDocuments } from './hooks/useDocuments'

export default function App() {
  const { documents, loading, error, refresh } = useDocuments()
  const [answer, setAnswer] = useState<AnswerModel | null>(null)

  return (
    <>
      <header className="masthead">
        <h1>DocQuery</h1>
        <p className="muted">Upload PDFs, then ask questions answered from their content with citations.</p>
      </header>
      <main className="columns">
        <section className="panel" aria-labelledby="documents-heading">
          <h2 id="documents-heading">Documents</h2>
          <UploadForm onUploaded={() => void refresh()} />
          <DocumentList documents={documents} loading={loading} error={error} />
        </section>
        <section className="panel" aria-labelledby="ask-heading">
          <h2 id="ask-heading">Ask</h2>
          <AskForm documents={documents} onAnswer={setAnswer} />
          {answer && <Answer answer={answer} />}
        </section>
      </main>
    </>
  )
}
