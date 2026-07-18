import { apiRequest } from './client'
import type { DiscountUsage, LowStockItem, SalesReport, TopProduct } from './types'

export function getSalesReport(signal?: AbortSignal) {
  return apiRequest<SalesReport>('/api/admin/reports/sales', { signal })
}

export function getTopProducts(limit: number, signal?: AbortSignal) {
  return apiRequest<TopProduct[]>('/api/admin/reports/top-products', { query: { limit }, signal })
}

export function getLowStock(threshold: number, signal?: AbortSignal) {
  return apiRequest<LowStockItem[]>('/api/admin/reports/low-stock', { query: { threshold }, signal })
}

export function getDiscountUsage(signal?: AbortSignal) {
  return apiRequest<DiscountUsage[]>('/api/admin/reports/discounts', { signal })
}
