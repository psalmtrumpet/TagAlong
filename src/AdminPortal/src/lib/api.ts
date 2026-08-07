import { getToken, clearToken } from './auth'

const BASE = 'https://www.tlimc.net'

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken()
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options.headers ?? {}),
    },
  })

  if (res.status === 401) {
    clearToken()
    window.location.href = '/admin/login'
    throw new Error('Unauthorized')
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error((body as { error?: string }).error ?? `HTTP ${res.status}`)
  }

  return res.json()
}

// ── Auth ──────────────────────────────────────────────────────────────────────

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  firstName: string
  lastName: string
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  return request('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

// ── Users ─────────────────────────────────────────────────────────────────────

export interface AdminUser {
  id: string
  authUserId: string
  email: string
  firstName: string
  lastName: string
  phoneNumber: string
  isVerified: boolean
  verificationStatus: string
  isSuspended: boolean
  isAvailable: boolean
  averageRating: number
  completedDeliveries: number
  completedTrips: number
  createdAt: string
}

export interface AdminUserDetail extends AdminUser {
  bio: string | null
  profileImageUrl: string | null
  verifiedAt: string | null
  identityDocumentUrl: string | null
  suspendedAt: string | null
  suspensionReason: string | null
  totalRatings: number
  kycNin: string | null
  kycFirstName: string | null
  kycLastName: string | null
  kycMiddleName: string | null
  kycDateOfBirth: string | null
  kycGender: string | null
  kycNationality: string | null
  kycResidenceState: string | null
  kycPhotoPath: string | null
  kycStatus: string | null
}

export interface UserListResult {
  users: AdminUser[]
  totalCount: number
  page: number
  pageSize: number
}

export interface UserStats {
  totalUsers: number
  verifiedUsers: number
  unverifiedUsers: number
  suspendedUsers: number
  activeUsers: number
}

export async function getUsers(page = 1, pageSize = 20, filter = 'all'): Promise<UserListResult> {
  return request(`/api/admin/users?page=${page}&pageSize=${pageSize}&filter=${filter}`)
}

export async function getUserDetail(authUserId: string): Promise<AdminUserDetail> {
  return request(`/api/admin/users/${authUserId}`)
}

export async function getUserStats(): Promise<UserStats> {
  return request('/api/admin/users/stats')
}

export async function suspendUser(authUserId: string, reason: string): Promise<void> {
  await request(`/api/admin/users/${authUserId}/suspend`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  })
}

export async function unsuspendUser(authUserId: string): Promise<void> {
  await request(`/api/admin/users/${authUserId}/unsuspend`, { method: 'POST' })
}

// ── Trips ─────────────────────────────────────────────────────────────────────

export interface AdminTrip {
  id: string
  travelerId: string
  origin: string
  originLatitude: number
  originLongitude: number
  destination: string
  destinationLatitude: number
  destinationLongitude: number
  departureTime: string
  status: string
  tripType: string
  availableCapacity: number
  vehicleType: string | null
  vehiclePlateNumber: string | null
  notes: string | null
  passengerCapacity: number
  currentPassengerCount: number
  currentLatitude: number | null
  currentLongitude: number | null
  createdAt: string
}

export interface TripListResult {
  trips: AdminTrip[]
  total: number
  page: number
  pageSize: number
}

export interface TripStats {
  total: number
  scheduled: number
  inProgress: number
  completed: number
  passenger: number
  delivery: number
}

export async function getTrips(page = 1, pageSize = 20, type = 'all', status = 'all'): Promise<TripListResult> {
  return request(`/api/admin/trips?page=${page}&pageSize=${pageSize}&type=${type}&status=${status}`)
}

export async function getTrip(id: string): Promise<AdminTrip> {
  return request(`/api/admin/trips/${id}`)
}

export async function getTripStats(): Promise<TripStats> {
  return request('/api/admin/trips/stats')
}

// ── Conversations ──────────────────────────────────────────────────────────────

export interface AdminConversation {
  id: string
  senderId: string
  travelerId: string
  senderName: string | null
  travelerName: string | null
  status: string
  agreedPrice: number | null
  recipientName: string | null
  startedAt: string | null
  deliveredAt: string | null
  createdAt: string
}

export interface AdminMessage {
  id: string
  conversationId: string
  senderId: string
  content: string
  messageType: string
  proposedPrice: number | null
  sentAt: string
}

export interface ConversationListResult {
  conversations: AdminConversation[]
  total: number
  page: number
  pageSize: number
}

export interface ConversationDetail {
  conversation: AdminConversation
  messages: AdminMessage[]
}

export interface ConversationStats {
  total: number
  pending: number
  active: number
  lockedIn: number
  inProgress: number
  closed: number
}

export async function getConversations(page = 1, pageSize = 20, status = 'all'): Promise<ConversationListResult> {
  return request(`/api/admin/conversations?page=${page}&pageSize=${pageSize}&status=${status}`)
}

export async function getConversation(id: string): Promise<ConversationDetail> {
  return request(`/api/admin/conversations/${id}`)
}

export async function getConversationStats(): Promise<ConversationStats> {
  return request('/api/admin/conversations/stats')
}
