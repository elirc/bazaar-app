/** Shared API response types, mirroring the server DTOs. */

export interface HealthStatus {
  status: string
  service: string
}

export interface Money {
  amount: number
  currency: string
}

export interface VariantOption {
  name: string
  value: string
}

export interface Variant {
  id: string
  sku: string
  title: string
  price: Money
  position: number
  options: VariantOption[]
  available: number
}

export interface ProductImage {
  id: string
  url: string
  altText: string | null
  position: number
}

export interface ProductSummary {
  id: string
  slug: string
  title: string
  vendor: string | null
  status: string
  imageUrl: string | null
  priceFrom: Money | null
  collections: string[]
}

export interface ProductDetail {
  id: string
  slug: string
  title: string
  description: string
  vendor: string | null
  status: string
  images: ProductImage[]
  variants: Variant[]
  collections: string[]
}

export interface Collection {
  id: string
  slug: string
  title: string
  description: string | null
  productCount: number
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}
