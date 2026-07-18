import { apiRequest } from './client'
import type { Cart, DiscountPreview, Order, ShippingOption } from './types'

export function getShippingOptions(cartToken: string, signal?: AbortSignal) {
  return apiRequest<ShippingOption[]>('/api/checkout/shipping-options', {
    query: { cartToken },
    signal,
  })
}

export function previewDiscount(code: string, subtotal: number, currency: string, signal?: AbortSignal) {
  return apiRequest<DiscountPreview>(`/api/storefront/discounts/${encodeURIComponent(code)}`, {
    query: { subtotal, currency },
    signal,
  })
}

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

export function setCartItemSaved(token: string, variantId: string, saved: boolean) {
  return apiRequest<Cart>(`/api/cart/${token}/items/${variantId}/saved`, {
    method: 'POST',
    body: { saved },
  })
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
  shippingMethodCode?: string
}

export function checkout(body: CheckoutBody) {
  return apiRequest<Order>('/api/checkout', { method: 'POST', body })
}

export function getOrder(id: string, signal?: AbortSignal) {
  return apiRequest<Order>(`/api/orders/${id}`, { signal })
}
