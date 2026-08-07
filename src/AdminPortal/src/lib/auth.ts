const TOKEN_KEY = 'tagalong_admin_token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export function isAdminToken(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      ?? payload['role']
      ?? payload['Role']
    return role === 'Admin'
  } catch {
    return false
  }
}

export function getTokenPayload(token: string): Record<string, unknown> | null {
  try {
    return JSON.parse(atob(token.split('.')[1]))
  } catch {
    return null
  }
}

export function getAdminName(token: string): string {
  const payload = getTokenPayload(token)
  if (!payload) return 'Admin'
  const given = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] as string ?? ''
  const surname = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] as string ?? ''
  return `${given} ${surname}`.trim() || 'Admin'
}
