import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getUserStats, getTripStats, getConversationStats, UserStats, TripStats, ConversationStats } from '../lib/api'

function StatCard({ label, value, sub, color }: { label: string; value: number | string; sub?: string; color: string }) {
  return (
    <div className="card p-5">
      <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-1">{label}</div>
      <div className={`text-3xl font-bold ${color}`}>{value}</div>
      {sub && <div className="text-xs text-gray-400 mt-1">{sub}</div>}
    </div>
  )
}

export default function DashboardPage() {
  const [users, setUsers]   = useState<UserStats | null>(null)
  const [trips, setTrips]   = useState<TripStats | null>(null)
  const [convos, setConvos] = useState<ConversationStats | null>(null)

  useEffect(() => {
    getUserStats().then(setUsers).catch(() => {})
    getTripStats().then(setTrips).catch(() => {})
    getConversationStats().then(setConvos).catch(() => {})
  }, [])

  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-gray-500 text-sm mt-1">Platform overview at a glance</p>
      </div>

      {/* User stats */}
      <section className="mb-8">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wider">Users</h2>
          <Link to="/users" className="text-sm text-brand-600 hover:underline">View all →</Link>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
          <StatCard label="Total Users"    value={users?.totalUsers    ?? '—'} color="text-gray-900" />
          <StatCard label="Verified"       value={users?.verifiedUsers  ?? '—'} color="text-green-600" />
          <StatCard label="Unverified"     value={users?.unverifiedUsers ?? '—'} color="text-amber-600" />
          <StatCard label="Suspended"      value={users?.suspendedUsers ?? '—'} color="text-red-600" />
          <StatCard label="Active Now"     value={users?.activeUsers    ?? '—'} sub="currently available" color="text-blue-600" />
        </div>
      </section>

      {/* Trip stats */}
      <section className="mb-8">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wider">Trips</h2>
          <Link to="/trips" className="text-sm text-brand-600 hover:underline">View all →</Link>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-6 gap-4">
          <StatCard label="Total"       value={trips?.total       ?? '—'} color="text-gray-900" />
          <StatCard label="Scheduled"   value={trips?.scheduled   ?? '—'} color="text-blue-600" />
          <StatCard label="In Progress" value={trips?.inProgress  ?? '—'} color="text-amber-600" />
          <StatCard label="Completed"   value={trips?.completed   ?? '—'} color="text-green-600" />
          <StatCard label="Passenger"   value={trips?.passenger   ?? '—'} color="text-gray-700" />
          <StatCard label="Delivery"    value={trips?.delivery    ?? '—'} color="text-gray-700" />
        </div>
      </section>

      {/* Conversation stats */}
      <section>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wider">Conversations</h2>
          <Link to="/conversations" className="text-sm text-brand-600 hover:underline">View all →</Link>
        </div>
        <div className="grid grid-cols-2 md:grid-cols-6 gap-4">
          <StatCard label="Total"       value={convos?.total       ?? '—'} color="text-gray-900" />
          <StatCard label="Pending"     value={convos?.pending     ?? '—'} color="text-amber-600" />
          <StatCard label="Active"      value={convos?.active      ?? '—'} color="text-blue-600" />
          <StatCard label="Locked In"   value={convos?.lockedIn    ?? '—'} color="text-purple-600" />
          <StatCard label="In Progress" value={convos?.inProgress  ?? '—'} color="text-orange-600" />
          <StatCard label="Closed"      value={convos?.closed      ?? '—'} color="text-green-600" />
        </div>
      </section>
    </div>
  )
}
