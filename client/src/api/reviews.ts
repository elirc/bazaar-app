import { apiRequest } from './client'
import type { AdminReview, PagedResult, Review } from './types'

export interface CreateReviewBody {
  rating: number
  title?: string
  body: string
}

export function listReviews(slug: string, signal?: AbortSignal) {
  return apiRequest<Review[]>(`/api/storefront/products/${encodeURIComponent(slug)}/reviews`, { signal })
}

export function createReview(slug: string, body: CreateReviewBody) {
  return apiRequest<Review>(`/api/storefront/products/${encodeURIComponent(slug)}/reviews`, {
    method: 'POST',
    body,
  })
}

export function markReviewHelpful(id: string) {
  return apiRequest<{ helpfulCount: number }>(`/api/storefront/reviews/${id}/helpful`, { method: 'POST' })
}

export interface AdminReviewQuery {
  status?: string
  page?: number
  pageSize?: number
}

export function listAdminReviews(query: AdminReviewQuery, signal?: AbortSignal) {
  return apiRequest<PagedResult<AdminReview>>('/api/admin/reviews', {
    query: { status: query.status, page: query.page, pageSize: query.pageSize },
    signal,
  })
}

export function moderateReview(id: string, status: string) {
  return apiRequest<{ id: string; status: string }>(`/api/admin/reviews/${id}/moderate`, {
    method: 'POST',
    body: { status },
  })
}
