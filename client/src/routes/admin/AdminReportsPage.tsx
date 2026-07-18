import { useQuery } from '@tanstack/react-query'
import { getDiscountUsage, getLowStock, getSalesReport, getTopProducts } from '../../api/reports'
import { formatMoney } from '../../lib/format'

export default function AdminReportsPage() {
  const sales = useQuery({ queryKey: ['report-sales'], queryFn: ({ signal }) => getSalesReport(signal) })
  const top = useQuery({ queryKey: ['report-top'], queryFn: ({ signal }) => getTopProducts(10, signal) })
  const lowStock = useQuery({ queryKey: ['report-low-stock'], queryFn: ({ signal }) => getLowStock(10, signal) })
  const discounts = useQuery({ queryKey: ['report-discounts'], queryFn: ({ signal }) => getDiscountUsage(signal) })

  const maxRevenue = Math.max(1, ...(sales.data?.buckets.map((b) => b.revenue.amount) ?? [1]))

  return (
    <section className="reports-page">
      <h1>Reports</h1>

      <div className="report-card">
        <h2>Sales over time</h2>
        {sales.data && (
          <>
            <p className="muted">
              {sales.data.totalOrders} orders · {formatMoney(sales.data.totalRevenue)} total
            </p>
            <table>
              <thead><tr><th>Date</th><th>Orders</th><th>Revenue</th><th aria-label="Chart" /></tr></thead>
              <tbody>
                {sales.data.buckets.map((bucket) => (
                  <tr key={bucket.date} data-testid="sales-row">
                    <td>{bucket.date}</td>
                    <td>{bucket.orderCount}</td>
                    <td>{formatMoney(bucket.revenue)}</td>
                    <td className="report-bar-cell">
                      <span
                        className="report-bar"
                        style={{ width: `${(bucket.revenue.amount / maxRevenue) * 100}%` }}
                        aria-hidden="true"
                      />
                    </td>
                  </tr>
                ))}
                {sales.data.buckets.length === 0 && <tr><td colSpan={4}>No sales yet.</td></tr>}
              </tbody>
            </table>
          </>
        )}
      </div>

      <div className="report-card">
        <h2>Top products</h2>
        {top.data && (
          <table>
            <thead><tr><th>Product</th><th>SKU</th><th>Sold</th><th>Revenue</th></tr></thead>
            <tbody>
              {top.data.map((product) => (
                <tr key={product.sku} data-testid="top-product-row">
                  <td>{product.title}</td>
                  <td>{product.sku}</td>
                  <td>{product.quantitySold}</td>
                  <td>{formatMoney(product.revenue)}</td>
                </tr>
              ))}
              {top.data.length === 0 && <tr><td colSpan={4}>No sales yet.</td></tr>}
            </tbody>
          </table>
        )}
      </div>

      <div className="report-card">
        <h2>Low stock</h2>
        {lowStock.data && (
          <table>
            <thead><tr><th>Product</th><th>SKU</th><th>Available</th></tr></thead>
            <tbody>
              {lowStock.data.map((item) => (
                <tr key={item.variantId} data-testid="low-stock-row">
                  <td>{item.productTitle}</td>
                  <td>{item.sku}</td>
                  <td>{item.available}</td>
                </tr>
              ))}
              {lowStock.data.length === 0 && <tr><td colSpan={3}>Everything is well stocked.</td></tr>}
            </tbody>
          </table>
        )}
      </div>

      <div className="report-card">
        <h2>Discount usage</h2>
        {discounts.data && (
          <table>
            <thead><tr><th>Code</th><th>Type</th><th>Used</th></tr></thead>
            <tbody>
              {discounts.data.map((discount) => (
                <tr key={discount.code} data-testid="discount-usage-row">
                  <td>{discount.code}</td>
                  <td>{discount.type}</td>
                  <td>{discount.timesUsed}{discount.usageLimit != null ? ` / ${discount.usageLimit}` : ''}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </section>
  )
}
