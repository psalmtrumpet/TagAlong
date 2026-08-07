import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { getUserDetail, suspendUser, unsuspendUser, AdminUserDetail } from '../lib/api'

function Field({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div>
      <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-0.5">{label}</div>
      <div className="text-sm text-gray-900">{value ?? <span className="text-gray-300">—</span>}</div>
    </div>
  )
}

function fmt(d?: string | null) {
  if (!d) return null
  return new Date(d).toLocaleDateString('en-NG', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export default function UserDetailPage() {
  const { authUserId } = useParams<{ authUserId: string }>()
  const navigate = useNavigate()
  const [user, setUser] = useState<AdminUserDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [action, setAction] = useState<'idle' | 'suspending' | 'unsuspending'>('idle')
  const [suspendReason, setSuspendReason] = useState('')
  const [showSuspendModal, setShowSuspendModal] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!authUserId) return
    getUserDetail(authUserId)
      .then(setUser)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [authUserId])

  const doSuspend = async () => {
    if (!authUserId || !suspendReason.trim()) return
    setAction('suspending')
    try {
      await suspendUser(authUserId, suspendReason)
      setUser(u => u ? { ...u, isSuspended: true, suspensionReason: suspendReason } : u)
      setShowSuspendModal(false)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed')
    } finally {
      setAction('idle')
    }
  }

  const doUnsuspend = async () => {
    if (!authUserId) return
    setAction('unsuspending')
    try {
      await unsuspendUser(authUserId)
      setUser(u => u ? { ...u, isSuspended: false, suspensionReason: null } : u)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed')
    } finally {
      setAction('idle')
    }
  }

  if (loading) return <div className="p-8 text-gray-400">Loading…</div>
  if (!user)   return <div className="p-8 text-red-500">User not found</div>

  const kycDone = user.kycStatus === 'Completed'

  return (
    <div className="p-8 max-w-5xl">
      {/* Header */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <button onClick={() => navigate(-1)} className="text-sm text-gray-400 hover:text-gray-700 mb-2">← Back</button>
          <h1 className="text-2xl font-bold text-gray-900">{user.firstName} {user.lastName}</h1>
          <div className="text-sm text-gray-400">{user.email} · {user.phoneNumber}</div>
        </div>
        <div className="flex gap-2">
          {user.isSuspended ? (
            <button className="btn-primary" disabled={action !== 'idle'} onClick={doUnsuspend}>
              {action === 'unsuspending' ? 'Unsuspending…' : 'Unsuspend User'}
            </button>
          ) : (
            <button className="btn-danger" disabled={action !== 'idle'} onClick={() => setShowSuspendModal(true)}>
              Suspend User
            </button>
          )}
        </div>
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-4">{error}</div>}

      {user.isSuspended && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 mb-6 text-sm text-red-800">
          <span className="font-semibold">Account suspended</span>
          {user.suspensionReason && ` — ${user.suspensionReason}`}
          {user.suspendedAt && ` (since ${fmt(user.suspendedAt)})`}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        {/* Profile */}
        <div className="card p-5 md:col-span-2">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">Profile</div>
          <div className="grid grid-cols-2 gap-4">
            <Field label="First Name"      value={user.firstName} />
            <Field label="Last Name"       value={user.lastName} />
            <Field label="Email"           value={user.email} />
            <Field label="Phone"           value={user.phoneNumber} />
            <Field label="Verified"        value={user.isVerified ? 'Yes' : 'No'} />
            <Field label="KYC Status"      value={user.verificationStatus} />
            <Field label="Verified At"     value={fmt(user.verifiedAt) ?? undefined} />
            <Field label="Joined"          value={fmt(user.createdAt) ?? undefined} />
            <Field label="Avg Rating"      value={`${user.averageRating.toFixed(2)} / 5.0 (${user.totalRatings} ratings)`} />
            <Field label="Trips Completed" value={`${user.completedTrips} rides · ${user.completedDeliveries} deliveries`} />
          </div>
        </div>

        {/* Quick stats */}
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">Status</div>
          <div className="space-y-3">
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Account</span>
              <span className={user.isSuspended ? 'text-red-600 font-medium' : 'text-green-600 font-medium'}>
                {user.isSuspended ? 'Suspended' : 'Active'}
              </span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Identity</span>
              <span className={user.isVerified ? 'text-green-600 font-medium' : 'text-amber-600 font-medium'}>
                {user.isVerified ? 'Verified' : 'Not verified'}
              </span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Availability</span>
              <span className={user.isAvailable ? 'text-green-600 font-medium' : 'text-gray-500'}>
                {user.isAvailable ? 'Available' : 'Offline'}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* KYC / Verification */}
      <div className="card p-5 mb-4">
        <div className="flex items-center justify-between mb-4">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider">KYC Verification Data</div>
          {kycDone
            ? <span className="badge-green">KYC Complete</span>
            : <span className="badge-amber">{user.kycStatus ?? 'Not submitted'}</span>}
        </div>

        {kycDone ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <Field label="NIN"              value={user.kycNin} />
            <Field label="Name (NIN)"       value={[user.kycFirstName, user.kycMiddleName, user.kycLastName].filter(Boolean).join(' ')} />
            <Field label="Date of Birth"    value={user.kycDateOfBirth} />
            <Field label="Gender"           value={user.kycGender} />
            <Field label="Nationality"      value={user.kycNationality} />
            <Field label="Residence State"  value={user.kycResidenceState} />
          </div>
        ) : (
          <p className="text-sm text-gray-400">No KYC data available for this user.</p>
        )}

        {/* NIN photo */}
        {user.kycPhotoPath && (
          <div className="mt-4">
            <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">NIN Photo</div>
            <img
              src={`https://www.tlimc.net/${user.kycPhotoPath.replace(/^\/?/, '')}`}
              alt="NIN photo"
              className="w-32 h-32 object-cover rounded-lg border border-gray-200"
              onError={e => { (e.target as HTMLImageElement).style.display = 'none' }}
            />
          </div>
        )}
      </div>

      {/* Suspend Modal */}
      {showSuspendModal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-md">
            <h2 className="text-lg font-bold text-gray-900 mb-2">Suspend {user.firstName} {user.lastName}?</h2>
            <p className="text-sm text-gray-500 mb-4">This user will not be able to log in while suspended.</p>
            <label className="block text-sm font-medium text-gray-700 mb-1">Reason for suspension</label>
            <textarea
              className="input resize-none mb-4"
              rows={3}
              placeholder="Suspicious activity, policy violation…"
              value={suspendReason}
              onChange={e => setSuspendReason(e.target.value)}
            />
            <div className="flex gap-2 justify-end">
              <button className="btn-secondary" onClick={() => setShowSuspendModal(false)}>Cancel</button>
              <button className="btn-danger" onClick={doSuspend} disabled={!suspendReason.trim() || action !== 'idle'}>
                {action === 'suspending' ? 'Suspending…' : 'Confirm Suspend'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
