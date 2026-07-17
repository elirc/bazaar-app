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

export interface CartLine {
  variantId: string
  productSlug: string
  productTitle: string
  variantTitle: string
  sku: string
  unitPrice: Money
  quantity: number
  lineTotal: Money
  available: number
}

export interface Cart {
  id: string
  token: string
  items: CartLine[]
  subtotal: Money
  itemCount: number
}

export interface Address {
  name: string
  line1: string
  line2: string | null
  city: string
  region: string | null
  postalCode: string
  country: string
}

export interface OrderLine {
  sku: string
  title: string
  quantity: number
  unitPrice: Money
  lineTotal: Money
}

export interface Order {
  id: string
  number: string
  email: string
  status: string
  currency: string
  shippingAddress: Address
  subtotal: Money
  discountTotal: Money
  taxTotal: Money
  shippingTotal: Money
  grandTotal: Money
  discountCode: string | null
  items: OrderLine[]
  placedAt: string
}

export interface OrderSummary {
  id: string
  number: string
  email: string
  status: string
  grandTotal: Money
  itemCount: number
  placedAt: string
}

export interface DiscountPreview {
  code: string
  valid: boolean
  reason: string | null
  discount: Money | null
}

export interface Discount {
  id: string
  code: string
  type: string
  value: number
  currency: string
  isActive: boolean
  startsAt: string | null
  endsAt: string | null
  usageLimit: number | null
  timesUsed: number
}
