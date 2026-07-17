import { Routes, Route, Navigate } from 'react-router-dom'
import StorefrontLayout from './routes/storefront/StorefrontLayout'
import HomePage from './routes/storefront/HomePage'
import AdminLayout from './routes/admin/AdminLayout'
import AdminDashboard from './routes/admin/AdminDashboard'

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<StorefrontLayout />}>
        <Route index element={<HomePage />} />
      </Route>
      <Route path="/admin" element={<AdminLayout />}>
        <Route index element={<AdminDashboard />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
