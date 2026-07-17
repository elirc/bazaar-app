import { describe, it, expect, vi, afterEach } from 'vitest'
import { apiRequest, ApiError } from './client'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('apiRequest', () => {
  it('parses a JSON response body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ ok: true }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await apiRequest<{ ok: boolean }>('/thing')

    expect(result).toEqual({ ok: true })
    expect(fetchMock).toHaveBeenCalledOnce()
  })

  it('appends query parameters, skipping empty values', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse([]))
    vi.stubGlobal('fetch', fetchMock)

    await apiRequest('/products', { query: { page: 2, search: '', inStock: true } })

    const calledUrl = String(fetchMock.mock.calls[0]?.[0])
    expect(calledUrl).toContain('page=2')
    expect(calledUrl).toContain('inStock=true')
    expect(calledUrl).not.toContain('search=')
  })

  it('throws ApiError carrying the status and ProblemDetails title', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({ title: 'Not found' }, 404))),
    )

    const error = await apiRequest('/missing').catch((err: unknown) => err)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 404, message: 'Not found' })
  })
})
