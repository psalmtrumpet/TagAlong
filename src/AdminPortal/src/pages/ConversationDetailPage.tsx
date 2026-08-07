import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { getConversation, ConversationDetail, AdminMessage } from '../lib/api'

function fmt(d?: string | null) {
  if (!d) return '—'
  return new Date(d).toLocaleString('en-NG', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function MessageBubble({ msg, senderId, travelerName, senderName }: {
  msg: AdminMessage; senderId: string; travelerName: string | null; senderName: string | null
}) {
  const isFromSender = msg.senderId === senderId
  const senderLabel = isFromSender ? (senderName ?? 'Sender') : (travelerName ?? 'Traveler')
  const align = isFromSender ? 'items-start' : 'items-end'
  const bubbleCls = isFromSender
    ? 'bg-gray-100 text-gray-900'
    : 'bg-brand-600 text-white'

  const isPriceMsg = msg.messageType === 'PriceProposal' || msg.messageType === 'PriceAccepted'

  return (
    <div className={`flex flex-col ${align} mb-3`}>
      <div className="text-xs text-gray-400 mb-1">{senderLabel} · {fmt(msg.sentAt)}</div>
      <div className={`rounded-xl px-4 py-2.5 max-w-sm text-sm ${bubbleCls}`}>
        {isPriceMsg && msg.proposedPrice != null && (
          <div className="font-bold text-base mb-0.5">
            ₦{Number(msg.proposedPrice).toLocaleString()}
            <span className="font-normal text-xs ml-1 opacity-75">
              {msg.messageType === 'PriceAccepted' ? '(accepted)' : '(proposed)'}
            </span>
          </div>
        )}
        {msg.messageType === 'LockIn' && (
          <div className="font-bold mb-0.5">🔒 Lock-In proposed</div>
        )}
        {msg.messageType === 'LockInConfirmed' && (
          <div className="font-bold mb-0.5">✅ Lock-In confirmed</div>
        )}
        {msg.content && msg.content !== 'null' && <div>{msg.content}</div>}
      </div>
    </div>
  )
}

export default function ConversationDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [data, setData] = useState<ConversationDetail | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!id) return
    getConversation(id).then(setData).catch(() => {}).finally(() => setLoading(false))
  }, [id])

  if (loading) return <div className="p-8 text-gray-400">Loading…</div>
  if (!data)   return <div className="p-8 text-red-500">Conversation not found</div>

  const { conversation: c, messages } = data

  const statusColor: Record<string, string> = {
    Pending: 'badge-amber', Active: 'badge-blue', LockedIn: 'badge-blue',
    InProgress: 'badge-amber', Closed: 'badge-green', Declined: 'badge-red',
  }

  return (
    <div className="p-8 max-w-5xl">
      <button onClick={() => navigate(-1)} className="text-sm text-gray-400 hover:text-gray-700 mb-4">← Back</button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Conversation Detail</h1>
          <div className="text-sm text-gray-400 font-mono mt-1">{c.id}</div>
        </div>
        <span className={statusColor[c.status] ?? 'badge-gray'}>{c.status}</span>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        {/* Parties */}
        <div className="card p-5 md:col-span-2">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">Parties</div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-xs text-gray-400 mb-0.5">Sender / Rider</div>
              <div className="font-medium text-gray-900">{c.senderName ?? '—'}</div>
              <div className="text-xs text-gray-400 font-mono">{c.senderId}</div>
            </div>
            <div>
              <div className="text-xs text-gray-400 mb-0.5">Traveler / Helper</div>
              <div className="font-medium text-gray-900">{c.travelerName ?? '—'}</div>
              <div className="text-xs text-gray-400 font-mono">{c.travelerId}</div>
            </div>
            {c.recipientName && (
              <div>
                <div className="text-xs text-gray-400 mb-0.5">Package Recipient</div>
                <div className="font-medium text-gray-900">{c.recipientName}</div>
              </div>
            )}
          </div>
        </div>

        {/* Financial */}
        <div className="card p-5">
          <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-4">Transaction</div>
          <div className="space-y-3">
            <div>
              <div className="text-xs text-gray-400">Agreed Price</div>
              <div className="text-xl font-bold text-gray-900">
                {c.agreedPrice != null ? `₦${Number(c.agreedPrice).toLocaleString()}` : '—'}
              </div>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-400">Started</span>
              <span className="text-gray-700">{fmt(c.startedAt)}</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-400">Delivered</span>
              <span className="text-gray-700">{fmt(c.deliveredAt)}</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-gray-400">Created</span>
              <span className="text-gray-700">{fmt(c.createdAt)}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Message thread */}
      <div className="card">
        <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider p-4 border-b border-gray-100">
          Message Thread ({messages.length})
        </div>
        <div className="p-4 max-h-[600px] overflow-y-auto space-y-1">
          {messages.length === 0 ? (
            <p className="text-sm text-gray-400 text-center py-8">No messages</p>
          ) : messages.map(m => (
            <MessageBubble
              key={m.id}
              msg={m}
              senderId={c.senderId}
              senderName={c.senderName}
              travelerName={c.travelerName}
            />
          ))}
        </div>
      </div>
    </div>
  )
}
