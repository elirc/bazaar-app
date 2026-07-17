import { Link } from 'react-router-dom'

export default function AdminDashboard() {
  return (
    <section className="admin-dashboard">
      <h1>Admin Dashboard</h1>
      <p>Manage your catalog and orders.</p>
      <div className="admin-cards">
        <Link to="/admin/products" className="admin-card">
          <h2>Products</h2>
          <p>Create, edit, and organise products and their variants.</p>
        </Link>
        <Link to="/admin/collections" className="admin-card">
          <h2>Collections</h2>
          <p>Group products into browsable collections.</p>
        </Link>
      </div>
    </section>
  )
}
