import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from './App'
import { ApiProvider } from './api/ApiProvider'
import { makeAnswer, makeDocument, makeFakeApi } from './test/fakeApi'

describe('App', () => {
  it('lists the documents on load', async () => {
    const api = makeFakeApi([makeDocument({ fileName: 'handbook.pdf' })])

    render(
      <ApiProvider client={api}>
        <App />
      </ApiProvider>,
    )

    expect(within(await screen.findByRole('table')).getByText('handbook.pdf')).toBeInTheDocument()
  })

  it('refreshes the list after an upload', async () => {
    const user = userEvent.setup()
    const api = makeFakeApi()
    api.listDocuments.mockResolvedValueOnce([]).mockResolvedValue([makeDocument({ id: 'doc-new', fileName: 'new.pdf', status: 'Embedded' })])
    render(
      <ApiProvider client={api}>
        <App />
      </ApiProvider>,
    )
    expect(await screen.findByText(/no documents yet/i)).toBeInTheDocument()

    await user.upload(screen.getByLabelText(/pdf file/i), new File(['%PDF'], 'new.pdf', { type: 'application/pdf' }))
    await user.click(screen.getByRole('button', { name: /upload/i }))

    expect(within(await screen.findByRole('table')).getByText('new.pdf')).toBeInTheDocument()
  })

  it('shows the answer with citations after asking', async () => {
    const user = userEvent.setup()
    const api = makeFakeApi([makeDocument()])
    api.askQuestion.mockResolvedValue(makeAnswer({ answer: 'Fourteen days [1].' }))
    render(
      <ApiProvider client={api}>
        <App />
      </ApiProvider>,
    )
    await screen.findByRole('table')

    await user.type(screen.getByLabelText(/question/i), 'How long?')
    await user.click(screen.getByRole('button', { name: /ask/i }))

    const answer = await screen.findByRole('region', { name: /answer/i })
    await waitFor(() => expect(answer).toHaveTextContent('Fourteen days'))
    expect(answer).toHaveTextContent('report.pdf, page 3')
  })
})
