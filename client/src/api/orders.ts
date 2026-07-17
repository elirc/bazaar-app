import { apiRequest } from './client'
import type { Discount, Order, OrderSummary, PagedResult } from './types'

export interface AdminOrderQuery {
  search?: string
  status?: string
  page?: number
  pageSize?: number
}

export function listAdminOrders(query: AdminOrderQuery, signal?: AbortSignal) {
  return apiRequest<PagedResult<OrderSummary>>('/api/admin/orders', {
    query: {
      search: query.search,
      status: query.status,
      page: query.page,
      pageSize: query.pageSize,
    },
    signal,
  })
}

export function getAdminOrder(id: string, signal?: AbortSignal) {
  return apiRequest<Order>(`/api/admin/orders/${id}`, { signal })
}

export function transitionOrder(id: string, status: string) {
  return apiRequest<Order>(`/api/admin/orders/${id}/transition`, {
    method: 'POST',
    body: { status },
  })
}

export interface CreateDiscountBody {
  code: string
  type: string
  value: number
  currency?: string
  isActive: boolean
  usageLimit?: number
}

export function listAdminDiscounts(signal?: AbortSignal) {
  return apiRequest<Discount[]>('/api/admin/discounts', { signal })
}

export function createAdminDiscount(body: CreateDiscountBody) {
  return apiRequest<Discount>('/api/admin/discounts', { method: 'POST', body })
}

export function deleteAdminDiscount(id: string) {
  return apiRequest<void>(`/api/admin/discounts/${id}`, { method: 'DELETE' })
}
