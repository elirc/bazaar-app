import { NavLink, Outlet } from 'react-router-dom'

export default function AdminLayout() {
  return (
    <div className="admin">
      <aside className="admin__sidebar">
        <h2 className="brand">Bazaar Admin</h2>
        <nav className="admin__nav">
          <NavLink to="/admin" end>Dashboard</NavLink>
          <NavLink to="/admin/products">Products</NavLink>
          <NavLink to="/admin/collections">Collections</NavLink>
          <NavLink to="/admin/orders">Orders</NavLink>
          <NavLink to="/admin/discounts">Discounts</NavLink>
          <NavLink to="/admin/reviews">Reviews</NavLink>
          <NavLink to="/">← Back to store</NavLink>
        </nav>
      </aside>
      <main className="admin__main">
        <Outlet />
      </main>
    </div>
  )
}
