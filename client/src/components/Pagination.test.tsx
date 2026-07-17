import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Pagination from './Pagination'

describe('Pagination', () => {
  it('disables Prev on the first page and pages forward', async () => {
    const onChange = vi.fn()
    render(<Pagination page={1} totalPages={3} hasPrevious={false} hasNext onChange={onChange} />)

    expect(screen.getByRole('button', { name: /prev/i })).toBeDisabled()
    expect(screen.getByText(/page 1 of 3/i)).toBeInTheDocument()

    await userEvent.setup().click(screen.getByRole('button', { name: /next/i }))
    expect(onChange).toHaveBeenCalledWith(2)
  })

  it('renders nothing when there is only one page', () => {
    const { container } = render(
      <Pagination page={1} totalPages={1} hasPrevious={false} hasNext={false} onChange={() => {}} />,
    )
    expect(container).toBeEmptyDOMElement()
  })
})
