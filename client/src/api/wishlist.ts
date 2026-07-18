import { apiRequest } from './client'
import type { Cart, Wishlist } from './types'

export function listWishlists(signal?: AbortSignal) {
  return apiRequest<Wishlist[]>('/api/account/wishlists', { signal })
}

export function createWishlist(name: string) {
  return apiRequest<Wishlist>('/api/account/wishlists', { method: 'POST', body: { name } })
}

export function deleteWishlist(id: string) {
  return apiRequest<void>(`/api/account/wishlists/${id}`, { method: 'DELETE' })
}

export function addToDefaultWishlist(variantId: string) {
  return apiRequest<Wishlist>('/api/account/wishlist/items', { method: 'POST', body: { variantId } })
}

export function addToWishlist(wishlistId: string, variantId: string) {
  return apiRequest<Wishlist>(`/api/account/wishlists/${wishlistId}/items`, {
    method: 'POST',
    body: { variantId },
  })
}

export function removeWishlistItem(wishlistId: string, variantId: string) {
  return apiRequest<Wishlist>(`/api/account/wishlists/${wishlistId}/items/${variantId}`, { method: 'DELETE' })
}

export function moveWishlistItemToCart(wishlistId: string, variantId: string, cartToken?: string) {
  return apiRequest<Cart>(`/api/account/wishlists/${wishlistId}/items/${variantId}/move-to-cart`, {
    method: 'POST',
    body: { cartToken },
  })
}
