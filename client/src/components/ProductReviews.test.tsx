import { describe, it, expect, vi, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders, jsonResponse } from '../test/utils'
import { AuthProvider } from '../auth/AuthContext'
import ProductReviews from './ProductReviews'
import { setAuthToken } from '../api/client'

const reviews = [
  {
    id: 'r1', authorName: 'Ada L.', rating: 5, title: 'Excellent', body: 'Fantastic mug.',
    isVerifiedPurchase: true, helpfulCount: 3, createdAt: new Date().toISOString(),
  },
]

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  setAuthToken(null)
})

describe('ProductReviews', () => {
  it('renders approved reviews with a verified badge and a sign-in prompt for guests', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse(reviews))))

    renderWithProviders(
      <AuthProvider>
        <ProductReviews slug="ceramic-mug" />
      </AuthProvider>,
    )

    await waitFor(() => expect(screen.getByText('Fantastic mug.')).toBeInTheDocument())
    expect(screen.getByText('Verified purchase')).toBeInTheDocument()
    expect(screen.getByText(/sign in/i)).toBeInTheDocument()
  })
})
