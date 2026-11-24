import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StatusBadge from '@/components/StatusBadge.vue'
import { fileSize, initials, timeAgo } from '@/utils/format'

describe('format helpers', () => {
  const now = new Date('2026-11-15T12:00:00Z')

  it('describes recent times relatively', () => {
    expect(timeAgo('2026-11-15T11:59:30Z', now)).toBe('just now')
    expect(timeAgo('2026-11-15T11:45:00Z', now)).toBe('15 min ago')
    expect(timeAgo('2026-11-15T09:00:00Z', now)).toBe('3 h ago')
    expect(timeAgo('2026-11-14T12:00:00Z', now)).toBe('1 day ago')
    expect(timeAgo('2026-11-01T12:00:00Z', now)).toContain('2026')
  })

  it('formats file sizes', () => {
    expect(fileSize(512)).toBe('512 B')
    expect(fileSize(2048)).toBe('2.0 KB')
    expect(fileSize(5 * 1024 * 1024)).toBe('5.0 MB')
  })

  it('builds initials from names', () => {
    expect(initials('Priya Shah')).toBe('PS')
    expect(initials('alex')).toBe('A')
    expect(initials('Mary Ann Lee Smith')).toBe('MA')
  })
})

describe('StatusBadge', () => {
  it('renders a readable label and colour for each status', () => {
    const review = mount(StatusBadge, { props: { status: 'InReview' } })
    expect(review.text()).toBe('In review')
    expect(review.classes()).toContain('bg-amber-50')

    const rejected = mount(StatusBadge, { props: { status: 'Rejected' } })
    expect(rejected.classes()).toContain('text-rose-700')
  })
})
