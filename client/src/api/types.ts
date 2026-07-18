/** Shared API response types, mirroring the server DTOs. */

export interface HealthStatus {
  status: string
  service: string
  checks?: { database?: string }
  timestamp?: string
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
  weightGrams: number
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
  averageRating: number | null
  reviewCount: number
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
  averageRating: number | null
  reviewCount: number
  taxCategory: string
}

export interface Review {
  id: string
  authorName: string
  rating: number
  title: string | null
  body: string
  isVerifiedPurchase: boolean
  helpfulCount: number
  createdAt: string
}

export interface AdminReview {
  id: string
  productId: string
  productTitle: string
  productSlug: string
  authorName: string
  rating: number
  title: string | null
  body: string
  status: string
  isVerifiedPurchase: boolean
  helpfulCount: number
  createdAt: string
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
  savedForLater: boolean
}

export interface Cart {
  id: string
  token: string
  items: CartLine[]
  subtotal: Money
  itemCount: number
  savedCount: number
}

export interface WishlistItem {
  variantId: string
  productSlug: string
  productTitle: string
  variantTitle: string
  sku: string
  price: Money
  available: number
  backInStock: boolean
  addedAt: string
}

export interface Wishlist {
  id: string
  name: string
  isDefault: boolean
  items: WishlistItem[]
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
  id: string
  variantId: string | null
  sku: string
  title: string
  quantity: number
  unitPrice: Money
  lineTotal: Money
}

export interface ReturnLine {
  orderLineItemId: string
  sku: string
  title: string
  quantity: number
}

export interface ReturnRequest {
  id: string
  orderId: string
  orderNumber: string
  status: string
  reason: string | null
  refundAmount: Money
  lines: ReturnLine[]
  createdAt: string
}

export interface AdminReturn extends ReturnRequest {
  email: string
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
  shippingMethod: string | null
  giftCardTotal: Money
  giftCardCode: string | null
  items: OrderLine[]
  placedAt: string
  shipments: Shipment[]
}

export interface ShipmentLine {
  orderLineItemId: string
  sku: string
  title: string
  quantity: number
}

export interface Shipment {
  id: string
  carrier: string
  trackingNumber: string
  shippedAt: string
  lines: ShipmentLine[]
}

export interface GiftCard {
  id: string
  code: string
  balance: Money
  initialBalance: Money
  isActive: boolean
  createdAt: string
}

export interface GiftCardBalance {
  code: string
  valid: boolean
  balance: Money | null
}

export interface ShippingOption {
  code: string
  name: string
  rateType: string
  cost: Money
  deliveryEstimate: string
  minDays: number
  maxDays: number
}

export interface CustomerAddress {
  id: string
  label: string | null
  isDefault: boolean
  address: Address
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

export interface CurrentUser {
  id: string
  email: string
  firstName: string | null
  lastName: string | null
  role: string
}

export interface AuthResponse {
  token: string
  expiresAt: string
  customer: CurrentUser
}

export interface SalesBucket {
  date: string
  orderCount: number
  revenue: Money
}

export interface SalesReport {
  buckets: SalesBucket[]
  totalOrders: number
  totalRevenue: Money
}

export interface TopProduct {
  sku: string
  title: string
  quantitySold: number
  revenue: Money
}

export interface LowStockItem {
  variantId: string
  sku: string
  productTitle: string
  available: number
}

export interface DiscountUsage {
  code: string
  type: string
  timesUsed: number
  usageLimit: number | null
}

export interface WebhookSubscription {
  id: string
  url: string
  events: string[]
  secret: string
  isActive: boolean
  createdAt: string
}

export interface WebhookDelivery {
  id: string
  subscriptionId: string
  event: string
  url: string
  success: boolean
  responseStatus: number | null
  attemptCount: number
  createdAt: string
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
