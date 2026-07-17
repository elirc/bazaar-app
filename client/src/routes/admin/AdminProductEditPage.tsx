import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createAdminProduct,
  getAdminProduct,
  listAdminCollections,
  updateAdminProduct,
  updateAdminVariant,
  type CreateProductBody,
  type UpdateProductBody,
} from '../../api/catalog'
import type { Collection, ProductDetail, Variant } from '../../api/types'
import { ApiError } from '../../api/client'

const STATUSES = ['Draft', 'Active', 'Archived']

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return 'Something went wrong.'
}

function CollectionPicker({
  collections,
  selected,
  onToggle,
}: {
  collections: Collection[]
  selected: string[]
  onToggle: (slug: string) => void
}) {
  if (collections.length === 0) return <p className="muted">No collections yet.</p>
  return (
    <div className="checkbox-group">
      {collections.map((c) => (
        <label key={c.id}>
          <input
            type="checkbox"
            checked={selected.includes(c.slug)}
            onChange={() => onToggle(c.slug)}
          />
          {c.title}
        </label>
      ))}
    </div>
  )
}

interface VariantDraft {
  sku: string
  title: string
  price: string
  stock: string
}

function CreateProduct({ collections }: { collections: Collection[] }) {
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [slug, setSlug] = useState('')
  const [description, setDescription] = useState('')
  const [vendor, setVendor] = useState('')
  const [status, setStatus] = useState('Draft')
  const [imageUrl, setImageUrl] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const [variants, setVariants] = useState<VariantDraft[]>([
    { sku: '', title: '', price: '', stock: '0' },
  ])

  const mutation = useMutation({
    mutationFn: (body: CreateProductBody) => createAdminProduct(body),
    onSuccess: (created) => navigate(`/admin/products/${created.id}`),
  })

  function updateVariant(index: number, patch: Partial<VariantDraft>) {
    setVariants((prev) => prev.map((v, i) => (i === index ? { ...v, ...patch } : v)))
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    const body: CreateProductBody = {
      title,
      slug,
      description: description || undefined,
      vendor: vendor || undefined,
      status,
      images: imageUrl ? [{ url: imageUrl, position: 0 }] : [],
      variants: variants.map((v) => ({
        sku: v.sku,
        title: v.title || undefined,
        price: Number(v.price),
        stockOnHand: Number(v.stock) || 0,
        options: [],
      })),
      collectionSlugs: selected,
    }
    mutation.mutate(body)
  }

  return (
    <section>
      <Link to="/admin/products">← Products</Link>
      <h1>New product</h1>
      {mutation.isError && <p className="error">{errorMessage(mutation.error)}</p>}
      <form className="admin-form" onSubmit={submit}>
        <label>
          Title
          <input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </label>
        <label>
          Slug
          <input
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            placeholder="lowercase-with-hyphens"
            required
          />
        </label>
        <label>
          Description
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
        </label>
        <label>
          Vendor
          <input value={vendor} onChange={(e) => setVendor(e.target.value)} />
        </label>
        <label>
          Status
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <label>
          Primary image URL
          <input value={imageUrl} onChange={(e) => setImageUrl(e.target.value)} placeholder="https://…" />
        </label>

        <fieldset>
          <legend>Collections</legend>
          <CollectionPicker
            collections={collections}
            selected={selected}
            onToggle={(s) =>
              setSelected((prev) => (prev.includes(s) ? prev.filter((x) => x !== s) : [...prev, s]))
            }
          />
        </fieldset>

        <fieldset>
          <legend>Variants</legend>
          {variants.map((v, i) => (
            <div className="variant-row" key={i}>
              <input
                aria-label={`Variant ${i + 1} SKU`}
                placeholder="SKU"
                value={v.sku}
                onChange={(e) => updateVariant(i, { sku: e.target.value })}
                required
              />
              <input
                aria-label={`Variant ${i + 1} title`}
                placeholder="Title"
                value={v.title}
                onChange={(e) => updateVariant(i, { title: e.target.value })}
              />
              <input
                aria-label={`Variant ${i + 1} price`}
                type="number"
                step="0.01"
                min="0"
                placeholder="Price"
                value={v.price}
                onChange={(e) => updateVariant(i, { price: e.target.value })}
                required
              />
              <input
                aria-label={`Variant ${i + 1} stock`}
                type="number"
                min="0"
                placeholder="Stock"
                value={v.stock}
                onChange={(e) => updateVariant(i, { stock: e.target.value })}
              />
              {variants.length > 1 && (
                <button type="button" onClick={() => setVariants((prev) => prev.filter((_, j) => j !== i))}>
                  Remove
                </button>
              )}
            </div>
          ))}
          <button
            type="button"
            onClick={() => setVariants((prev) => [...prev, { sku: '', title: '', price: '', stock: '0' }])}
          >
            + Add variant
          </button>
        </fieldset>

        <button type="submit" className="primary" disabled={mutation.isPending}>
          {mutation.isPending ? 'Creating…' : 'Create product'}
        </button>
      </form>
    </section>
  )
}

function VariantRow({ productId, variant }: { productId: string; variant: Variant }) {
  const queryClient = useQueryClient()
  const [price, setPrice] = useState(String(variant.price.amount))
  const [stock, setStock] = useState(String(variant.available))

  const mutation = useMutation({
    mutationFn: () =>
      updateAdminVariant(variant.id, {
        price: Number(price),
        currency: variant.price.currency,
        stockOnHand: Number(stock),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin-product', productId] }),
  })

  return (
    <tr>
      <td>{variant.sku}</td>
      <td>{variant.title}</td>
      <td>
        <input
          aria-label={`Price for ${variant.sku}`}
          type="number"
          step="0.01"
          min="0"
          value={price}
          onChange={(e) => setPrice(e.target.value)}
        />
      </td>
      <td>
        <input
          aria-label={`Stock for ${variant.sku}`}
          type="number"
          min="0"
          value={stock}
          onChange={(e) => setStock(e.target.value)}
        />
      </td>
      <td>
        <button type="button" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : 'Save'}
        </button>
      </td>
    </tr>
  )
}

function EditProductForm({ product, collections }: { product: ProductDetail; collections: Collection[] }) {
  const queryClient = useQueryClient()
  const [title, setTitle] = useState(product.title)
  const [description, setDescription] = useState(product.description)
  const [vendor, setVendor] = useState(product.vendor ?? '')
  const [status, setStatus] = useState(product.status)
  const [imageUrl, setImageUrl] = useState(product.images[0]?.url ?? '')
  const [selected, setSelected] = useState<string[]>(product.collections)
  const [saved, setSaved] = useState(false)

  const mutation = useMutation({
    mutationFn: (body: UpdateProductBody) => updateAdminProduct(product.id, body),
    onSuccess: (updated) => {
      queryClient.setQueryData(['admin-product', product.id], updated)
      queryClient.invalidateQueries({ queryKey: ['admin-products'] })
      setSaved(true)
    },
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setSaved(false)
    mutation.mutate({
      title,
      description: description || undefined,
      vendor: vendor || undefined,
      status,
      images: imageUrl ? [{ url: imageUrl, position: 0 }] : [],
      collectionSlugs: selected,
    })
  }

  return (
    <section>
      <Link to="/admin/products">← Products</Link>
      <h1>Edit: {product.title}</h1>
      <p className="muted">/{product.slug}</p>
      {mutation.isError && <p className="error">{errorMessage(mutation.error)}</p>}
      {saved && <p className="success" role="status">Saved.</p>}

      <form className="admin-form" onSubmit={submit}>
        <label>
          Title
          <input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </label>
        <label>
          Description
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
        </label>
        <label>
          Vendor
          <input value={vendor} onChange={(e) => setVendor(e.target.value)} />
        </label>
        <label>
          Status
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <label>
          Primary image URL
          <input value={imageUrl} onChange={(e) => setImageUrl(e.target.value)} placeholder="https://…" />
        </label>
        <fieldset>
          <legend>Collections</legend>
          <CollectionPicker
            collections={collections}
            selected={selected}
            onToggle={(s) =>
              setSelected((prev) => (prev.includes(s) ? prev.filter((x) => x !== s) : [...prev, s]))
            }
          />
        </fieldset>
        <button type="submit" className="primary" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : 'Save changes'}
        </button>
      </form>

      <h2>Variants</h2>
      <table>
        <thead>
          <tr>
            <th>SKU</th>
            <th>Title</th>
            <th>Price</th>
            <th>Stock</th>
            <th aria-label="Actions" />
          </tr>
        </thead>
        <tbody>
          {product.variants.map((v) => (
            <VariantRow key={v.id} productId={product.id} variant={v} />
          ))}
        </tbody>
      </table>
    </section>
  )
}

function EditProduct({ id, collections }: { id: string; collections: Collection[] }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin-product', id],
    queryFn: ({ signal }) => getAdminProduct(id, signal),
  })

  if (isLoading) return <p>Loading…</p>
  if (isError || !data) return <p className="error">Could not load product.</p>
  return <EditProductForm key={data.id} product={data} collections={collections} />
}

export default function AdminProductEditPage() {
  const { id } = useParams<{ id: string }>()
  const collectionsQuery = useQuery({
    queryKey: ['admin-collections'],
    queryFn: ({ signal }) => listAdminCollections(signal),
  })
  const collections = collectionsQuery.data ?? []

  return id ? <EditProduct id={id} collections={collections} /> : <CreateProduct collections={collections} />
}
