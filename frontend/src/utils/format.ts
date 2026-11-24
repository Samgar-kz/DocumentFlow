import type { AuditAction, DocumentStatus } from '@/api/types'

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('en-CA', { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-CA', { month: 'short', day: 'numeric', year: 'numeric' })
}

/** "just now", "5 min ago", "3 h ago", "2 days ago", then a date. */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((now.getTime() - new Date(iso).getTime()) / 1000)
  if (seconds < 60) return 'just now'
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes} min ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours} h ago`
  const days = Math.round(hours / 24)
  if (days < 7) return `${days} day${days === 1 ? '' : 's'} ago`
  return formatDate(iso)
}

export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export const statusLabel: Record<DocumentStatus, string> = {
  Draft: 'Draft',
  InReview: 'In review',
  Approved: 'Approved',
  Rejected: 'Rejected',
}

export const actionLabel: Record<AuditAction, string> = {
  DocumentCreated: 'Created',
  DetailsUpdated: 'Edited details',
  VersionUploaded: 'Uploaded a version',
  VersionDownloaded: 'Downloaded',
  Submitted: 'Submitted for approval',
  StepApproved: 'Approved a step',
  Approved: 'Fully approved',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrew',
  CommentAdded: 'Commented',
  Deleted: 'Deleted',
  UserRoleChanged: 'Changed a role',
}

export function initials(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}
