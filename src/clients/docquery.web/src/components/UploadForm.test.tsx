import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiProvider } from '../api/ApiProvider'
import { ApiError } from '../api/client'
import { makeFakeApi } from '../test/fakeApi'
import { UploadForm } from './UploadForm'

function renderForm(api = makeFakeApi()) {
  const onUploaded = vi.fn()
  render(
    <ApiProvider client={api}>
      <UploadForm onUploaded={onUploaded} />
    </ApiProvider>,
  )
  return { api, onUploaded }
}

describe('UploadForm', () => {
  it('uploads the chosen file and reports the new document id', async () => {
    const user = userEvent.setup()
    const { api, onUploaded } = renderForm()
    const file = new File(['%PDF-1.4'], 'report.pdf', { type: 'application/pdf' })

    await user.upload(screen.getByLabelText(/pdf file/i), file)
    await user.click(screen.getByRole('button', { name: /upload/i }))

    await waitFor(() => expect(onUploaded).toHaveBeenCalledWith('doc-new'))
    expect(api.uploadDocument).toHaveBeenCalledWith(file)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('asks for a file when none is chosen', async () => {
    const user = userEvent.setup()
    const { api } = renderForm()

    await user.click(screen.getByRole('button', { name: /upload/i }))

    expect(screen.getByRole('alert')).toHaveTextContent(/choose a pdf/i)
    expect(api.uploadDocument).not.toHaveBeenCalled()
  })

  it('shows the API problem details when the upload is rejected', async () => {
    const user = userEvent.setup()
    const api = makeFakeApi()
    api.uploadDocument.mockRejectedValue(new ApiError(413, 'File too large', 'The limit is 50 MB.'))
    const { onUploaded } = renderForm(api)

    await user.upload(screen.getByLabelText(/pdf file/i), new File(['x'], 'big.pdf', { type: 'application/pdf' }))
    await user.click(screen.getByRole('button', { name: /upload/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('File too large: The limit is 50 MB.')
    expect(onUploaded).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: /upload/i })).toBeEnabled()
  })

  it('mentions the retry interval on rate limiting', async () => {
    const user = userEvent.setup()
    const api = makeFakeApi()
    api.uploadDocument.mockRejectedValue(new ApiError(429, 'Too many uploads', undefined, 60))
    renderForm(api)

    await user.upload(screen.getByLabelText(/pdf file/i), new File(['x'], 'a.pdf', { type: 'application/pdf' }))
    await user.click(screen.getByRole('button', { name: /upload/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Retry in 60 s.')
  })
})
