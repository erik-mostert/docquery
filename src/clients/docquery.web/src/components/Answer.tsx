import type { ReactNode } from 'react'
import type { Answer as AnswerModel } from '../api/types'

interface Props {
  answer: AnswerModel
}

/** The answer with every "[n]" turned into a link to citation n, followed by the citations themselves. */
export function Answer({ answer }: Props) {
  return (
    <section className="answer" aria-label="Answer">
      <p className="answer-text">{renderWithCitationLinks(answer.answer)}</p>
      {answer.citations.length > 0 && (
        <ol className="citations">
          {answer.citations.map((citation) => (
            <li key={citation.number} id={`citation-${citation.number}`}>
              <span className="citation-source">
                {citation.fileName}, page {citation.pageNumber}
              </span>
              <span className="muted"> · similarity {Math.round(citation.score * 100)}%</span>
              <blockquote>{citation.excerpt}</blockquote>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}

const citationPattern = /\[(\d+)\]/g

function renderWithCitationLinks(text: string) {
  const parts: ReactNode[] = []
  let last = 0
  for (const match of text.matchAll(citationPattern)) {
    const index = match.index
    if (index > last) {
      parts.push(text.slice(last, index))
    }
    parts.push(
      <a key={`${index}-${match[1]}`} href={`#citation-${match[1]}`} className="citation-link">
        [{match[1]}]
      </a>,
    )
    last = index + match[0].length
  }
  if (last < text.length) {
    parts.push(text.slice(last))
  }
  return parts
}
