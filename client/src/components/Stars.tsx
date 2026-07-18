/** Compact star-rating display. Rounds to the nearest whole star for the glyphs. */
export default function Stars({
  rating,
  count,
}: {
  rating: number | null
  count?: number
}) {
  if (rating == null || (count != null && count === 0)) {
    return <span className="stars stars--empty muted">No reviews yet</span>
  }
  const rounded = Math.round(rating)
  const filled = '★'.repeat(rounded)
  const empty = '☆'.repeat(5 - rounded)
  return (
    <span className="stars" aria-label={`Rated ${rating} out of 5`}>
      <span className="stars__glyphs" aria-hidden="true">{filled}{empty}</span>
      <span className="stars__value">{rating.toFixed(1)}</span>
      {count != null && <span className="stars__count muted"> ({count})</span>}
    </span>
  )
}
