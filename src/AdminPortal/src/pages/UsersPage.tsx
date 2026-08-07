import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getUsers, AdminUser } from '../lib/api'
import Pagination from '../components/Pagination'

const FILTERS = ['all', 'verified', 'unverified', 'suspended']

function VerificationBadge({ status, isVerified }: { status: string; isVerified: boolean }) {
  if (isVerified) return <span className="badge-green">Verified</span>
  if (status === 'Pending') return <span className="badge-amber">Pending</span>
  return <span className="badge-gray">Unverified</span>
}

function fmt(date: string) {
  return new Date(date).toLocaleDateString('en-NG', { day: '2-digit', month: 'short', year: 'numeric' })
}

export default function UsersPage() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage]   = useState(1)
  const [filter, setFilter] = useState('all')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  const PAGE_SIZE = 20

  useEffect(() => {
    setLoading(true)
    getUsers(page, PAGE_SIZE, filter)
      .then(res => { setUsers(res.users); setTotal(res.totalCount) })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [page, filter])

  const filtered = search
    ? users.filter(u =>
        `${u.firstName} ${u.lastName} ${u.email} ${u.phoneNumber}`
          .toLowerCase()
          .includes(search.toLowerCase()))
    : users

  return (
    <div className="p-8">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Users</h1>
          <p className="text-gray-500 text-sm mt-1">{total.toLocaleString()} total registered users</p>
        </div>
      </div>

      {/* Filters + Search */}
      <div className="card mb-4">
        <div className="flex items-center gap-4 p-4 border-b border-gray-100">
          <div className="flex gap-1">
            {FILTERS.map(f => (
              <button
                key={f}
                onClick={() => { setFilter(f); setPage(1) }}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium capitalize transition-colors ${
                  filter === f ? 'bg-brand-600 text-white' : 'text-gray-600 hover:bg-gray-100'
                }`}
              >
                {f}
              </button>
            ))}
          </div>
          <input
            className="input max-w-xs ml-auto"
            placeholder="Search name, email, phone…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
                <th className="px-4 py-3 text-left">User</th>
                <th className="px-4 py-3 text-left">Phone</th>
                <th className="px-4 py-3 text-left">Verification</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Rating</th>
                <th className="px-4 py-3 text-left">Trips</th>
                <th className="px-4 py-3 text-left">Joined</th>
                <th className="px-4 py-3 text-left"></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={8} className="text-center py-12 text-gray-400">Loading…</td></tr>
              ) : filtered.length === 0 ? (
                <tr><td colSpan={8} className="text-center py-12 text-gray-400">No users found</td></tr>
              ) : filtered.map(u => (
                <tr key={u.authUserId} className="border-b border-gray-50 hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{u.firstName} {u.lastName}</div>
                    <div className="text-xs text-gray-400">{u.email}</div>
                  </td>
                  <td className="px-4 py-3 text-gray-600">{u.phoneNumber}</td>
                  <td className="px-4 py-3">
                    <VerificationBadge status={u.verificationStatus} isVerified={u.isVerified} />
                  </td>
                  <td className="px-4 py-3">
                    {u.isSuspended
                      ? <span className="badge-red">Suspended</span>
                      : u.isAvailable
                      ? <span className="badge-green">Available</span>
                      : <span className="badge-gray">Offline</span>
                    }
                  </td>
                  <td className="px-4 py-3 text-gray-600">{u.averageRating.toFixed(1)} ★</td>
                  <td className="px-4 py-3 text-gray-600">{u.completedTrips + u.completedDeliveries}</td>
                  <td className="px-4 py-3 text-gray-400">{fmt(u.createdAt)}</td>
                  <td className="px-4 py-3">
                    <Link to={`/users/${u.authUserId}`} className="text-brand-600 hover:underline text-xs font-medium">
                      View →
                    </Link>
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
