import { apiRequest } from './client'
import type { AdminReturn, PagedResult, ReturnRequest } from './types'

export interface CreateReturnLine {
  orderLineItemId: string
  quantity: number
}

export interface CreateReturnBody {
  reason?: string
  lines: CreateReturnLine[]
}

export function createReturn(orderId: string, body: CreateReturnBody) {
  return apiRequest<ReturnRequest>(`/api/account/orders/${orderId}/returns`, { method: 'POST', body })
}

export function listAccountReturns(signal?: AbortSignal) {
  return apiRequest<ReturnRequest[]>('/api/account/returns', { signal })
}

export interface AdminReturnQuery {
  status?: string
  page?: number
  pageSize?: number
}

export function listAdminReturns(query: AdminReturnQuery, signal?: AbortSignal) {
  return apiRequest<PagedResult<AdminReturn>>('/api/admin/returns', {
    query: { status: query.status, page: query.page, pageSize: query.pageSize },
    signal,
  })
}

export function approveReturn(id: string) {
  return apiRequest<AdminReturn>(`/api/admin/returns/${id}/approve`, { method: 'POST' })
}

export function rejectReturn(id: string, reason?: string) {
  return apiRequest<AdminReturn>(`/api/admin/returns/${id}/reject`, { method: 'POST', body: { reason } })
}
