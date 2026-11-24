// Types mirroring the DocumentFlow API contracts (enums are serialised as strings).

export type UserRole = 'Member' | 'Admin'
export type DocumentStatus = 'Draft' | 'InReview' | 'Approved' | 'Rejected'
export type DocumentCategory = 'Contract' | 'Invoice' | 'Memo' | 'Policy' | 'Report' | 'Other'
export type StepStatus = 'Pending' | 'Approved' | 'Rejected' | 'Skipped'
export type DocumentScope = 'Mine' | 'AwaitingMe' | 'Involved' | 'All'
export type NotificationType = 'ApprovalRequested' | 'DocumentApproved' | 'DocumentRejected' | 'CommentAdded'
export type AuditAction =
  | 'DocumentCreated'
  | 'DetailsUpdated'
  | 'VersionUploaded'
  | 'VersionDownloaded'
  | 'Submitted'
  | 'StepApproved'
  | 'Approved'
  | 'Rejected'
  | 'Withdrawn'
  | 'CommentAdded'
  | 'Deleted'
  | 'UserRoleChanged'

export const CATEGORIES: DocumentCategory[] = ['Contract', 'Invoice', 'Memo', 'Policy', 'Report', 'Other']
export const STATUSES: DocumentStatus[] = ['Draft', 'InReview', 'Approved', 'Rejected']

export interface User {
  id: string
  email: string
  fullName: string
  department: string
  role: UserRole
}

export interface AuthResponse {
  accessToken: string
  expiresAt: string
  user: User
}

export interface UserRef {
  id: string
  fullName: string
  department: string
}

export interface DirectoryUser extends UserRef {
  email: string
}

export interface DocumentSummary {
  id: string
  title: string
  category: DocumentCategory
  status: DocumentStatus
  owner: UserRef
  versionCount: number
  currentApprover: UserRef | null
  createdAt: string
  lastActivityAt: string
}

export interface DocumentVersion {
  number: number
  fileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  uploadedBy: UserRef
  uploadedAt: string
  comment: string | null
}

export interface ApprovalStep {
  order: number
  approver: UserRef
  status: StepStatus
  comment: string | null
  decidedAt: string | null
  isCurrent: boolean
}

export interface DocumentComment {
  id: string
  author: UserRef
  body: string
  createdAt: string
}

export interface DocumentPermissions {
  canEdit: boolean
  canUploadVersion: boolean
  canSubmit: boolean
  canWithdraw: boolean
  canDecide: boolean
  canDelete: boolean
  canComment: boolean
}

export interface DocumentDetail {
  id: string
  title: string
  description: string | null
  category: DocumentCategory
  status: DocumentStatus
  owner: UserRef
  createdAt: string
  submittedAt: string | null
  completedAt: string | null
  versions: DocumentVersion[]
  steps: ApprovalStep[]
  comments: DocumentComment[]
  permissions: DocumentPermissions
}

export interface Activity {
  id: string
  action: AuditAction
  actor: UserRef | null
  documentId: string | null
  documentTitle: string | null
  details: string
  occurredAt: string
}

export interface DocumentQuery {
  scope?: DocumentScope
  status?: DocumentStatus
  category?: DocumentCategory
  search?: string
  page?: number
  pageSize?: number
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface Notification {
  id: string
  type: NotificationType
  documentId: string
  message: string
  isRead: boolean
  createdAt: string
}

export interface NotificationFeed {
  unreadCount: number
  items: Notification[]
}

export interface Dashboard {
  awaitingMyApproval: number
  myDrafts: number
  myInReview: number
  myApprovedLast30Days: number
  awaiting: DocumentSummary[]
  recentlyUpdated: DocumentSummary[]
  activity: Activity[]
}

export interface AdminUser {
  id: string
  email: string
  fullName: string
  department: string
  role: UserRole
  createdAt: string
  lastLoginAt: string | null
}

export interface AuditQuery {
  action?: AuditAction
  from?: string
  to?: string
  page?: number
  pageSize?: number
}
