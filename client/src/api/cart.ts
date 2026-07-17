import { apiRequest } from './client'
import type { Cart, Order } from './types'

export function createCart() {
  return apiRequest<Cart>('/api/cart', { method: 'POST' })
}

export function getCart(token: string, signal?: AbortSignal) {
  return apiRequest<Cart>(`/api/cart/${token}`, { signal })
}

export function addCartItem(token: string, variantId: string, quantity: number) {
  return apiRequest<Cart>(`/api/cart/${token}/items`, {
    method: 'POST',
    body: { variantId, quantity },
  })
}

export function updateCartItem(token: string, variantId: string, quantity: number) {
  return apiRequest<Cart>(`/api/cart/${token}/items/${variantId}`, {
    method: 'PUT',
    body: { quantity },
  })
}

export function removeCartItem(token: string, variantId: string) {
  return apiRequest<Cart>(`/api/cart/${token}/items/${variantId}`, { method: 'DELETE' })
}

export interface AddressInput {
  name: string
  line1: string
  line2?: string
  city: string
  region?: string
  postalCode: string
  country: string
}

export interface CheckoutBody {
  cartToken: string
  email: string
  shippingAddress: AddressInput
  discountCode?: string
}

export function checkout(body: CheckoutBody) {
  return apiRequest<Order>('/api/checkout', { method: 'POST', body })
}

export function getOrder(id: string, signal?: AbortSignal) {
  return apiRequest<Order>(`/api/orders/${id}`, { signal })
}
