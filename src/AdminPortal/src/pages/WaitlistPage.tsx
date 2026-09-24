import { useEffect, useState, useRef } from 'react'
import { getWaitlist, WaitlistEntry } from '../lib/api'
import * as XLSX from 'xlsx'

function fmt(date: string) {
  return new Date(date).toLocaleString('en-NG', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  })
}

export default function WaitlistPage() {
  const [entries, setEntries] = useState<WaitlistEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const fetchedRef = useRef(false)

  useEffect(() => {
    if (fetchedRef.current) return
    fetchedRef.current = true
    getWaitlist()
      .then(setEntries)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  const filtered = search
    ? entries.filter(e =>
        `${e.name} ${e.email} ${e.phone}`.toLowerCase().includes(search.toLowerCase()))
    : entries

  function exportExcel() {
    const rows = filtered.map((e, i) => ({
      '#': i + 1,
      Name: e.name,
      Email: e.email,
      Phone: e.phone || '—',
      'Joined At': new Date(e.joinedAt).toLocaleString('en-NG'),
    }))
    const ws = XLSX.utils.json_to_sheet(rows)
    ws['!cols'] = [{ wch: 5 }, { wch: 28 }, { wch: 34 }, { wch: 18 }, { wch: 22 }]
    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Waitlist')
    XLSX.writeFile(wb, `tagalong-waitlist-${new Date().toISOString().slice(0, 10)}.xlsx`)
  }

  return (
    <div className="p-8">
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Waitlist</h1>
          <p className="text-gray-500 text-sm mt-1">
            {loading ? 'Loading…' : `${entries.length.toLocaleString()} signups`}
          </p>
        </div>
        <button
          onClick={exportExcel}
          disabled={loading || entries.length === 0}
          className="flex items-center gap-2 px-4 py-2 bg-brand-600 text-white rounded-lg text-sm font-medium hover:bg-brand-700 disabled:opacity-40 transition-colors"
        >
          <span>↓</span> Export Excel
        </button>
      </div>

      <div className="card">
        <div className="p-4 border-b border-gray-100">
          <input
            className="input max-w-xs"
            placeholder="Search name, email, phone…"
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-xs uppercase tracking-wider text-gray-400 border-b border-gray-100">
                <th className="px-4 py-3 text-left">#</th>
                <th className="px-4 py-3 text-left">Name</th>
                <th className="px-4 py-3 text-left">Email</th>
                <th className="px-4 py-3 text-left">Phone</th>
                <th className="px-4 py-3 text-left">Signed up</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {loading && (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center text-gray-400">Loading…</td>
                </tr>
              )}
              {!loading && filtered.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center text-gray-400">No entries found</td>
                </tr>
              )}
              {filtered.map((e, i) => (
                <tr key={e.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3 text-gray-400 tabular-nums">{i + 1}</td>
                  <td className="px-4 py-3 font-medium text-gray-900">{e.name}</td>
                  <td className="px-4 py-3 text-gray-600">{e.email}</td>
                  <td className="px-4 py-3 text-gray-500">{e.phone || '—'}</td>
                  <td className="px-4 py-3 text-gray-500">{fmt(e.joinedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
