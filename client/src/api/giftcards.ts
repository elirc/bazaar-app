import { apiRequest } from './client'
import type { GiftCard, GiftCardBalance } from './types'

export function checkGiftCardBalance(code: string, signal?: AbortSignal) {
  return apiRequest<GiftCardBalance>(`/api/storefront/gift-cards/${encodeURIComponent(code)}`, { signal })
}

export function listAdminGiftCards(signal?: AbortSignal) {
  return apiRequest<GiftCard[]>('/api/admin/gift-cards', { signal })
}

export interface IssueGiftCardBody {
  amount: number
  code?: string
}

export function issueGiftCard(body: IssueGiftCardBody) {
  return apiRequest<GiftCard>('/api/admin/gift-cards', { method: 'POST', body })
}
