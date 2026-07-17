import { Link, Outlet } from 'react-router-dom'
import ApiStatus from '../../components/ApiStatus'

export default function StorefrontLayout() {
  return (
    <div className="storefront">
      <header className="storefront__header">
        <Link to="/" className="brand">Bazaar</Link>
        <nav className="storefront__nav">
          <Link to="/">Shop</Link>
          <Link to="/admin">Admin</Link>
        </nav>
      </header>
      <main className="storefront__main">
        <Outlet />
      </main>
      <footer className="storefront__footer">
        <span>Bazaar — a Shopify-Lite demo storefront</span>
        <ApiStatus />
      </footer>
    </div>
  )
}
