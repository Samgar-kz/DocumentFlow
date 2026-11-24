<script setup lang="ts">
import type { DocumentStatus } from '@/api/types'
import { statusLabel } from '@/utils/format'

defineProps<{ status: DocumentStatus }>()

const styles: Record<DocumentStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700 ring-slate-200',
  InReview: 'bg-amber-50 text-amber-800 ring-amber-200',
  Approved: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Rejected: 'bg-rose-50 text-rose-700 ring-rose-200',
}
</script>

<template>
  <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset" :class="styles[status]">
    {{ statusLabel[status] }}
  </span>
</template>
