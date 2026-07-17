import { Link, Outlet } from 'react-router-dom'

export default function AdminLayout() {
  return (
    <div className="admin">
      <aside className="admin__sidebar">
        <h2 className="brand">Bazaar Admin</h2>
        <nav className="admin__nav">
          <Link to="/admin">Dashboard</Link>
          <Link to="/">← Back to store</Link>
        </nav>
      </aside>
      <main className="admin__main">
        <Outlet />
      </main>
    </div>
  )
}
