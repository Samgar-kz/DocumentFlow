<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { Activity, AuditAction } from '@/api/types'
import { actionLabel, initials, timeAgo } from '@/utils/format'

defineProps<{ items: Activity[]; showDocument?: boolean }>()

const tone: Partial<Record<AuditAction, string>> = {
  Approved: 'bg-emerald-100 text-emerald-700',
  StepApproved: 'bg-emerald-100 text-emerald-700',
  Rejected: 'bg-rose-100 text-rose-700',
  Submitted: 'bg-amber-100 text-amber-800',
  Deleted: 'bg-rose-100 text-rose-700',
}
</script>

<template>
  <ol class="space-y-4">
    <li v-for="item in items" :key="item.id" class="flex gap-3">
      <span
        class="grid size-8 shrink-0 place-items-center rounded-full text-xs font-semibold"
        :class="tone[item.action] ?? 'bg-slate-100 text-slate-600'"
      >
        {{ initials(item.actor?.fullName ?? '?') }}
      </span>
      <div class="min-w-0 text-sm">
        <p class="text-slate-800">
          <span class="font-medium">{{ item.actor?.fullName ?? 'Someone' }}</span>
          <span class="text-slate-500"> · {{ actionLabel[item.action] }}</span>
        </p>
        <p class="text-slate-600">{{ item.details }}</p>
        <p class="text-xs text-slate-400">
          {{ timeAgo(item.occurredAt) }}
          <template v-if="showDocument && item.documentTitle">
            ·
            <RouterLink v-if="item.documentId" :to="`/documents/${item.documentId}`" class="hover:text-brand-700">
              {{ item.documentTitle }}
            </RouterLink>
          </template>
        </p>
      </div>
    </li>
    <li v-if="items.length === 0" class="text-sm text-slate-500">No activity yet.</li>
  </ol>
</template>
