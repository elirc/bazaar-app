import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createReview, listReviews, markReviewHelpful } from '../api/reviews'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import Stars from './Stars'

export default function ProductReviews({ slug }: { slug: string }) {
  const { isAuthenticated } = useAuth()
  const queryClient = useQueryClient()

  const [rating, setRating] = useState(5)
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [submitted, setSubmitted] = useState(false)

  const reviewsQuery = useQuery({
    queryKey: ['reviews', slug],
    queryFn: ({ signal }) => listReviews(slug, signal),
  })

  const createMutation = useMutation({
    mutationFn: () => createReview(slug, { rating, title: title || undefined, body }),
    onSuccess: () => {
      setSubmitted(true)
      setTitle('')
      setBody('')
    },
  })

  const helpfulMutation = useMutation({
    mutationFn: (id: string) => markReviewHelpful(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['reviews', slug] }),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    createMutation.mutate()
  }

  const error = createMutation.error instanceof ApiError ? createMutation.error.message : null

  return (
    <section className="reviews">
      <h2>Customer reviews</h2>

      {reviewsQuery.isLoading && <p>Loading reviews…</p>}
      {reviewsQuery.data && reviewsQuery.data.length === 0 && <p className="muted">No reviews yet. Be the first!</p>}
      {reviewsQuery.data && reviewsQuery.data.length > 0 && (
        <ul className="review-list">
          {reviewsQuery.data.map((review) => (
            <li key={review.id} className="review" data-testid="review">
              <div className="review__head">
                <Stars rating={review.rating} />
                <strong>{review.title ?? 'Review'}</strong>
                {review.isVerifiedPurchase && <span className="badge">Verified purchase</span>}
              </div>
              <p className="review__body">{review.body}</p>
              <div className="review__foot muted">
                <span>{review.authorName}</span>
                <button
                  type="button"
                  className="link"
                  onClick={() => helpfulMutation.mutate(review.id)}
                  disabled={helpfulMutation.isPending || !isAuthenticated}
                >
                  Helpful ({review.helpfulCount})
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <div className="review-form">
        <h3>Write a review</h3>
        {!isAuthenticated && (
          <p className="muted">
            <Link to="/login">Sign in</Link> to review a product you've purchased.
          </p>
        )}
        {isAuthenticated && submitted && (
          <p className="success" role="status">Thanks! Your review is awaiting moderation.</p>
        )}
        {isAuthenticated && !submitted && (
          <form className="admin-form" onSubmit={submit}>
            <label>
              Rating
              <select value={rating} onChange={(e) => setRating(Number(e.target.value))}>
                {[5, 4, 3, 2, 1].map((n) => (
                  <option key={n} value={n}>{n} star{n === 1 ? '' : 's'}</option>
                ))}
              </select>
            </label>
            <label>
              Title
              <input value={title} onChange={(e) => setTitle(e.target.value)} />
            </label>
            <label>
              Review
              <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={4} required />
            </label>
            {error && <p className="error" role="alert">{error}</p>}
            <button type="submit" className="primary" disabled={createMutation.isPending || !body.trim()}>
              {createMutation.isPending ? 'Submitting…' : 'Submit review'}
            </button>
          </form>
        )}
      </div>
    </section>
  )
}
