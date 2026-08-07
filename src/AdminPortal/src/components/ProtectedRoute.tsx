import { Navigate } from 'react-router-dom'
import { getToken, isAdminToken } from '../lib/auth'

export default function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const token = getToken()
  if (!token || !isAdminToken(token)) return <Navigate to="/login" replace />
  return <>{children}</>
}
