import { apiRequest } from './client'
import type { WebhookDelivery, WebhookSubscription } from './types'

export const WEBHOOK_EVENTS = ['order.created', 'order.paid', 'order.fulfilled', 'order.refunded']

export function listWebhooks(signal?: AbortSignal) {
  return apiRequest<WebhookSubscription[]>('/api/admin/webhooks', { signal })
}

export interface CreateWebhookBody {
  url: string
  events: string[]
  secret?: string
}

export function createWebhook(body: CreateWebhookBody) {
  return apiRequest<WebhookSubscription>('/api/admin/webhooks', { method: 'POST', body })
}

export function deleteWebhook(id: string) {
  return apiRequest<void>(`/api/admin/webhooks/${id}`, { method: 'DELETE' })
}

export function listWebhookDeliveries(subscriptionId?: string, signal?: AbortSignal) {
  return apiRequest<WebhookDelivery[]>('/api/admin/webhooks/deliveries', {
    query: { subscriptionId },
    signal,
  })
}
