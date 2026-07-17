interface PaginationProps {
  page: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
  onChange: (page: number) => void
}

export default function Pagination({ page, totalPages, hasPrevious, hasNext, onChange }: PaginationProps) {
  if (totalPages <= 1) return null
  return (
    <nav className="pagination" aria-label="Pagination">
      <button type="button" disabled={!hasPrevious} onClick={() => onChange(page - 1)}>
        ← Prev
      </button>
      <span className="pagination__status">
        Page {page} of {totalPages}
      </span>
      <button type="button" disabled={!hasNext} onClick={() => onChange(page + 1)}>
        Next →
      </button>
    </nav>
  )
}
