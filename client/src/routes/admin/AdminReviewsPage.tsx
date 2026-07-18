import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listAdminReviews, moderateReview } from '../../api/reviews'
import Stars from '../../components/Stars'

const STATUSES = ['Pending', 'Approved', 'Rejected']

export default function AdminReviewsPage() {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('Pending')

  const reviewsQuery = useQuery({
    queryKey: ['admin-reviews', status],
    queryFn: ({ signal }) => listAdminReviews({ status }, signal),
  })

  const moderateMutation = useMutation({
    mutationFn: ({ id, target }: { id: string; target: string }) => moderateReview(id, target),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-reviews'] }),
  })

  return (
    <section>
      <h1>Review moderation</h1>

      <div className="admin-filters">
        <label>
          Status
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            {STATUSES.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </label>
      </div>

      {reviewsQuery.isLoading && <p>Loading…</p>}
      {reviewsQuery.data && (
        <table>
          <thead>
            <tr>
              <th>Product</th><th>Rating</th><th>Review</th><th>Author</th><th>Verified</th><th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {reviewsQuery.data.items.map((r) => (
              <tr key={r.id} data-testid="admin-review-row">
                <td>{r.productTitle}</td>
                <td><Stars rating={r.rating} /></td>
                <td>
                  {r.title && <strong>{r.title}</strong>}
                  <div className="muted">{r.body}</div>
                </td>
                <td>{r.authorName}</td>
                <td>{r.isVerifiedPurchase ? 'Yes' : 'No'}</td>
                <td className="row-actions">
                  {r.status !== 'Approved' && (
                    <button
                      type="button"
                      onClick={() => moderateMutation.mutate({ id: r.id, target: 'Approved' })}
                      disabled={moderateMutation.isPending}
                    >
                      Approve
                    </button>
                  )}
                  {r.status !== 'Rejected' && (
                    <button
                      type="button"
                      onClick={() => moderateMutation.mutate({ id: r.id, target: 'Rejected' })}
                      disabled={moderateMutation.isPending}
                    >
                      Reject
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {reviewsQuery.data.items.length === 0 && (
              <tr><td colSpan={6}>No reviews in this state.</td></tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  )
}
