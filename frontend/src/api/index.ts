import { del, get, http, post, put, request, toQuery } from './http'
import type {
  Activity,
  AdminUser,
  AuditQuery,
  AuthResponse,
  Dashboard,
  DirectoryUser,
  DocumentCategory,
  DocumentDetail,
  DocumentQuery,
  DocumentSummary,
  NotificationFeed,
  PagedResult,
  User,
  UserRole,
} from './types'

export const authApi = {
  login: (email: string, password: string) => post<AuthResponse>('/auth/login', { email, password }),
  register: (body: { email: string; password: string; fullName: string; department: string }) =>
    post<AuthResponse>('/auth/register', body),
  refresh: () => post<AuthResponse>('/auth/refresh'),
  logout: () => post<void>('/auth/logout'),
  me: () => get<User>('/auth/me'),
}

export interface NewDocument {
  title: string
  description: string
  category: DocumentCategory
  file: File
}

export const documentsApi = {
  search: (query: DocumentQuery) => get<PagedResult<DocumentSummary>>(`/documents${toQuery(query)}`),
  get: (id: string) => get<DocumentDetail>(`/documents/${id}`),
  create(document: NewDocument) {
    const form = new FormData()
    form.append('title', document.title)
    form.append('description', document.description)
    form.append('category', document.category)
    form.append('file', document.file)
    return http<DocumentDetail>('/documents', { method: 'POST', body: form })
  },
  update: (id: string, body: { title: string; description: string | null; category: DocumentCategory }) =>
    put<DocumentDetail>(`/documents/${id}`, body),
  uploadVersion(id: string, file: File, comment: string) {
    const form = new FormData()
    form.append('file', file)
    if (comment) form.append('comment', comment)
    return http<DocumentDetail>(`/documents/${id}/versions`, { method: 'POST', body: form })
  },
  submit: (id: string, approverIds: string[]) => post<DocumentDetail>(`/documents/${id}/submit`, { approverIds }),
  approve: (id: string, comment: string | null) => post<DocumentDetail>(`/documents/${id}/approve`, { comment }),
  reject: (id: string, comment: string) => post<DocumentDetail>(`/documents/${id}/reject`, { comment }),
  withdraw: (id: string) => post<DocumentDetail>(`/documents/${id}/withdraw`),
  comment: (id: string, body: string) => post<DocumentDetail>(`/documents/${id}/comments`, { body }),
  activity: (id: string) => get<Activity[]>(`/documents/${id}/activity`),
  remove: (id: string) => del(`/documents/${id}`),
  /** Downloads a version with the bearer token and hands it to the browser as a file. */
  async download(id: string, number: number, fileName: string) {
    const response = await request(`/documents/${id}/versions/${number}/download`)
    const url = URL.createObjectURL(await response.blob())
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    link.click()
    URL.revokeObjectURL(url)
  },
}

export const workspaceApi = {
  dashboard: () => get<Dashboard>('/dashboard'),
  directory: (search?: string) => get<DirectoryUser[]>(`/users${toQuery({ search })}`),
  notifications: () => get<NotificationFeed>('/notifications'),
  markRead: (id: string) => post<void>(`/notifications/${id}/read`),
  markAllRead: () => post<void>('/notifications/read-all'),
}

export const adminApi = {
  audit: (query: AuditQuery) => get<PagedResult<Activity>>(`/admin/audit${toQuery(query)}`),
  users: () => get<AdminUser[]>('/admin/users'),
  changeRole: (id: string, role: UserRole) => put<User>(`/admin/users/${id}/role`, { role }),
}
