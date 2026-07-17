import { apiRequest } from './client'
import type {
  Collection,
  PagedResult,
  ProductDetail,
  ProductSummary,
  Variant,
} from './types'

// ---- Request bodies (mirror server request DTOs) ----

export interface VariantOptionInput {
  name: string
  value: string
}

export interface VariantInput {
  sku: string
  title?: string
  price: number
  currency?: string
  stockOnHand: number
  options: VariantOptionInput[]
}

export interface ImageInput {
  url: string
  altText?: string
  position: number
}

export interface CreateProductBody {
  title: string
  slug: string
  description?: string
  vendor?: string
  status?: string
  images: ImageInput[]
  variants: VariantInput[]
  collectionSlugs: string[]
}

export interface UpdateProductBody {
  title: string
  description?: string
  vendor?: string
  status?: string
  images: ImageInput[]
  collectionSlugs: string[]
}

export interface UpdateVariantBody {
  title?: string
  price: number
  currency?: string
  stockOnHand?: number
}

export interface CollectionBody {
  title: string
  slug: string
  description?: string
}

// ---- Storefront ----

export interface StorefrontProductQuery {
  search?: string
  collection?: string
  sort?: string
  page?: number
  pageSize?: number
}

export function listStorefrontProducts(query: StorefrontProductQuery, signal?: AbortSignal) {
  return apiRequest<PagedResult<ProductSummary>>('/api/storefront/products', {
    query: {
      search: query.search,
      collection: query.collection,
      sort: query.sort,
      page: query.page,
      pageSize: query.pageSize,
    },
    signal,
  })
}

export function getStorefrontProduct(slug: string, signal?: AbortSignal) {
  return apiRequest<ProductDetail>(`/api/storefront/products/${encodeURIComponent(slug)}`, { signal })
}

export function listStorefrontCollections(signal?: AbortSignal) {
  return apiRequest<Collection[]>('/api/storefront/collections', { signal })
}

// ---- Admin ----

export interface AdminProductQuery {
  search?: string
  status?: string
  page?: number
  pageSize?: number
}

export function listAdminProducts(query: AdminProductQuery, signal?: AbortSignal) {
  return apiRequest<PagedResult<ProductSummary>>('/api/admin/products', {
    query: {
      search: query.search,
      status: query.status,
      page: query.page,
      pageSize: query.pageSize,
    },
    signal,
  })
}

export function getAdminProduct(id: string, signal?: AbortSignal) {
  return apiRequest<ProductDetail>(`/api/admin/products/${id}`, { signal })
}

export function createAdminProduct(body: CreateProductBody) {
  return apiRequest<ProductDetail>('/api/admin/products', { method: 'POST', body })
}

export function updateAdminProduct(id: string, body: UpdateProductBody) {
  return apiRequest<ProductDetail>(`/api/admin/products/${id}`, { method: 'PUT', body })
}

export function deleteAdminProduct(id: string) {
  return apiRequest<void>(`/api/admin/products/${id}`, { method: 'DELETE' })
}

export function updateAdminVariant(id: string, body: UpdateVariantBody) {
  return apiRequest<Variant>(`/api/admin/variants/${id}`, { method: 'PUT', body })
}

export function listAdminCollections(signal?: AbortSignal) {
  return apiRequest<Collection[]>('/api/admin/collections', { signal })
}

export function createAdminCollection(body: CollectionBody) {
  return apiRequest<Collection>('/api/admin/collections', { method: 'POST', body })
}

export function deleteAdminCollection(id: string) {
  return apiRequest<void>(`/api/admin/collections/${id}`, { method: 'DELETE' })
}
