import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createAdminCollection,
  deleteAdminCollection,
  listAdminCollections,
} from '../../api/catalog'
import { ApiError } from '../../api/client'

export default function AdminCollectionsPage() {
  const queryClient = useQueryClient()
  const [title, setTitle] = useState('')
  const [slug, setSlug] = useState('')
  const [description, setDescription] = useState('')

  const collectionsQuery = useQuery({
    queryKey: ['admin-collections'],
    queryFn: ({ signal }) => listAdminCollections(signal),
  })

  const createMutation = useMutation({
    mutationFn: () => createAdminCollection({ title, slug, description: description || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-collections'] })
      setTitle('')
      setSlug('')
      setDescription('')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAdminCollection(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-collections'] }),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    createMutation.mutate()
  }

  const message = createMutation.error instanceof ApiError ? createMutation.error.message : null

  return (
    <section>
      <h1>Collections</h1>

      <form className="admin-form inline" onSubmit={submit}>
        <input aria-label="Collection title" placeholder="Title" value={title} onChange={(e) => setTitle(e.target.value)} required />
        <input
          aria-label="Collection slug"
          placeholder="slug"
          value={slug}
          onChange={(e) => setSlug(e.target.value)}
          required
        />
        <input aria-label="Collection description" placeholder="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
        <button type="submit" className="primary" disabled={createMutation.isPending}>
          Add
        </button>
      </form>
      {message && <p className="error">{message}</p>}

      {collectionsQuery.isLoading && <p>Loading…</p>}
      {collectionsQuery.data && (
        <table>
          <thead>
            <tr>
              <th>Title</th>
              <th>Slug</th>
              <th>Products</th>
              <th aria-label="Actions" />
            </tr>
          </thead>
          <tbody>
            {collectionsQuery.data.map((c) => (
              <tr key={c.id} data-testid="collection-row">
                <td>{c.title}</td>
                <td>{c.slug}</td>
                <td>{c.productCount}</td>
                <td>
                  <button type="button" onClick={() => deleteMutation.mutate(c.id)} disabled={deleteMutation.isPending}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
            {collectionsQuery.data.length === 0 && (
              <tr>
                <td colSpan={4}>No collections yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </section>
  )
}
