import { Routes, Route, Navigate } from 'react-router-dom'
import StorefrontLayout from './routes/storefront/StorefrontLayout'
import ProductListPage from './routes/storefront/ProductListPage'
import ProductDetailPage from './routes/storefront/ProductDetailPage'
import CheckoutPage from './routes/storefront/CheckoutPage'
import OrderConfirmationPage from './routes/storefront/OrderConfirmationPage'
import AdminLayout from './routes/admin/AdminLayout'
import AdminDashboard from './routes/admin/AdminDashboard'
import AdminProductsPage from './routes/admin/AdminProductsPage'
import AdminProductEditPage from './routes/admin/AdminProductEditPage'
import AdminCollectionsPage from './routes/admin/AdminCollectionsPage'
import AdminOrdersPage from './routes/admin/AdminOrdersPage'
import AdminOrderDetailPage from './routes/admin/AdminOrderDetailPage'
import AdminDiscountsPage from './routes/admin/AdminDiscountsPage'

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<StorefrontLayout />}>
        <Route index element={<ProductListPage />} />
        <Route path="products/:slug" element={<ProductDetailPage />} />
        <Route path="checkout" element={<CheckoutPage />} />
        <Route path="order/:id" element={<OrderConfirmationPage />} />
      </Route>
      <Route path="/admin" element={<AdminLayout />}>
        <Route index element={<AdminDashboard />} />
        <Route path="products" element={<AdminProductsPage />} />
        <Route path="products/new" element={<AdminProductEditPage />} />
        <Route path="products/:id" element={<AdminProductEditPage />} />
        <Route path="collections" element={<AdminCollectionsPage />} />
        <Route path="orders" element={<AdminOrdersPage />} />
        <Route path="orders/:id" element={<AdminOrderDetailPage />} />
        <Route path="discounts" element={<AdminDiscountsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
