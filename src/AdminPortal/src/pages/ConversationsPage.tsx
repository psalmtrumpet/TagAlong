import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getConversations, AdminConversation } from '../lib/api'
import Pagination from '../components/Pagination'

const STATUSES = ['all', 'Pending', 'Active', 'LockedIn', 'InProgress', 'Closed', 'Declined']

function statusBadge(s: string) {
  const map: Record<string, string> = {
    Pending: 'badge-amber', Active: 'badge-blue', LockedIn: 'badge-blue',
    InProgress: 'badge-amber', Closed: 'badge-green', Declined: 'badge-red',
  }
  return <span className={map[s] ?? 'badge-gray'}>{s}</span>
}

function fmt(d: string) {
  return new Date(d).toLocaleDateString('en-NG', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
}

export default function ConversationsPage() {
  const [convos, setConvos] = useState<AdminConversation[]>([])
  const [total, setTotal]   = useState(0)
  const [page, setPage]     = useState(1)
  const [status, setStatus] = useState('all')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  const PAGE_SIZE = 20

  useEffect(() => {
    setLoading(true)
    getConversations(page, PAGE_SIZE, status)
      .then(res => { setConvos(res.conversations); setTotal(res.total) })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [page, status])

  const filtered = search
    ? convos.filter(c => `${c.senderName ?? ''} ${c.travelerName ?? ''} ${c.recipientName ?? ''}`.toLowerCase().includes(search.toLowerCase()))
    : convos

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Conversations</h1>
        <p className="text-gray-500 text-sm mt-1">{total.toLocaleString()} total conversations (bookings &amp; negotiations)</p>
      </div>

      <div className="card">
        <div className="flex flex-wrap items-center gap-3 p-4 border-b border-gray-100">
          <div className="flex flex-wrap gap-1">
            {STATUSES.map(s => (
              <button key={s} onClick={() => { setStatus(s); setPage(1) }}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium capitalize transition-colors ${status === s ? 'bg-brand-600 text-white' : 'text-gray-600 hover:bg-gray-100'}`}>
                {s}
              </button>
            ))}
          </div>
          <input
            className="input max-w-xs ml-auto"
            placeholder="Search by name…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
                <th className="px-4 py-3 text-left">Sender (Rider)</th>
                <th className="px-4 py-3 text-left">Traveler (Helper)</th>
                <th className="px-4 py-3 text-left">Recipient</th>
                <th className="px-4 py-3 text-left">Price</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Started</th>
                <th className="px-4 py-3 text-left">Delivered</th>
                <th className="px-4 py-3 text-left">Created</th>
                <th className="px-4 py-3 text-left"></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={9} className="text-center py-12 text-gray-400">Loading…</td></tr>
              ) : filtered.length === 0 ? (
                <tr><td colSpan={9} className="text-center py-12 text-gray-400">No conversations found</td></tr>
              ) : filtered.map(c => (
                <tr key={c.id} className="border-b border-gray-50 hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3 font-medium text-gray-900">{c.senderName ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-700">{c.travelerName ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-500">{c.recipientName ?? '—'}</td>
                  <td className="px-4 py-3 font-medium text-gray-900">
                    {c.agreedPrice != null ? `₦${Number(c.agreedPrice).toLocaleString()}` : '—'}
                  </td>
                  <td className="px-4 py-3">{statusBadge(c.status)}</td>
                  <td className="px-4 py-3 text-gray-400">{c.startedAt ? fmt(c.startedAt) : '—'}</td>
                  <td className="px-4 py-3 text-gray-400">{c.deliveredAt ? fmt(c.deliveredAt) : '—'}</td>
                  <td className="px-4 py-3 text-gray-400">{fmt(c.createdAt)}</td>
                  <td className="px-4 py-3">
                    <Link to={`/conversations/${c.id}`} className="text-brand-600 hover:underline text-xs font-medium">View →</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Pagination page={page} pageSize={PAGE_SIZE} total={total} onChange={setPage} />
      </div>
    </div>
  )
}
