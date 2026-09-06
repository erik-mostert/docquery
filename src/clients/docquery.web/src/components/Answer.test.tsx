import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { makeAnswer } from '../test/fakeApi'
import { Answer } from './Answer'

describe('Answer', () => {
  it('turns citation markers into links to the matching citation', () => {
    render(<Answer answer={makeAnswer({ answer: 'Two weeks [1], by card [2].' })} />)

    const links = screen.getAllByRole('link')
    expect(links.map((link) => link.textContent)).toEqual(['[1]', '[2]'])
    expect(links[0]).toHaveAttribute('href', '#citation-1')
    expect(links[1]).toHaveAttribute('href', '#citation-2')
    expect(screen.getByText(/two weeks/i)).toBeInTheDocument()
  })

  it('lists each citation with source, page, similarity and excerpt', () => {
    render(<Answer answer={makeAnswer()} />)

    const item = screen.getByRole('listitem')
    expect(item).toHaveAttribute('id', 'citation-1')
    expect(item).toHaveTextContent('report.pdf, page 3')
    expect(item).toHaveTextContent('similarity 91%')
    expect(item).toHaveTextContent('Refunds are issued within 14 days.')
  })

  it('renders an answer without citations as plain text', () => {
    render(<Answer answer={{ answer: 'No embedded document content is available.', citations: [] }} />)

    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
    expect(screen.getByText(/no embedded document content/i)).toBeInTheDocument()
  })
})
