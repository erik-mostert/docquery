import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiProvider } from '../api/ApiProvider'
import { ApiError } from '../api/client'
import { makeAnswer, makeDocument, makeFakeApi } from '../test/fakeApi'
import { AskForm } from './AskForm'

const documents = [
  makeDocument({ id: 'ready', fileName: 'ready.pdf', status: 'Embedded' }),
  makeDocument({ id: 'pending', fileName: 'pending.pdf', status: 'Chunked' }),
]

function renderForm(api = makeFakeApi()) {
  const onAnswer = vi.fn()
  render(
    <ApiProvider client={api}>
      <AskForm documents={documents} onAnswer={onAnswer} />
    </ApiProvider>,
  )
  return { api, onAnswer }
}

describe('AskForm', () => {
  it('offers only embedded documents as a scope, plus all documents', () => {
    renderForm()

    const options = screen.getAllByRole('option').map((option) => option.textContent)
    expect(options).toEqual(['All documents', 'ready.pdf'])
  })

  it('sends the trimmed question over all documents and hands back the answer', async () => {
    const user = userEvent.setup()
    const { api, onAnswer } = renderForm()

    await user.type(screen.getByLabelText(/question/i), '  How long do refunds take?  ')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() => expect(onAnswer).toHaveBeenCalledWith(makeAnswer()))
    expect(api.askQuestion).toHaveBeenCalledWith({ question: 'How long do refunds take?', documentId: undefined })
  })

  it('scopes the question to the selected document', async () => {
    const user = userEvent.setup()
    const { api } = renderForm()

    await user.type(screen.getByLabelText(/question/i), 'question')
    await user.selectOptions(screen.getByLabelText(/search in/i), 'ready')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    await waitFor(() => expect(api.askQuestion).toHaveBeenCalledWith({ question: 'question', documentId: 'ready' }))
  })

  it('refuses an empty question without calling the API', async () => {
    const user = userEvent.setup()
    const { api } = renderForm()

    await user.click(screen.getByRole('button', { name: /ask/i }))

    expect(screen.getByRole('alert')).toHaveTextContent(/type a question/i)
    expect(api.askQuestion).not.toHaveBeenCalled()
  })

  it('shows the API error and re-enables the form', async () => {
    const user = userEvent.setup()
    const api = makeFakeApi()
    api.askQuestion.mockRejectedValue(new ApiError(503, 'Service temporarily unavailable', 'A dependency is unavailable.', 30))
    renderForm(api)

    await user.type(screen.getByLabelText(/question/i), 'question')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Service temporarily unavailable: A dependency is unavailable. Retry in 30 s.')
    expect(screen.getByRole('button', { name: /ask/i })).toBeEnabled()
  })
})
