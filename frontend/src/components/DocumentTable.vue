<script setup lang="ts">
import { RouterLink } from 'vue-router'
import type { DocumentSummary } from '@/api/types'
import StatusBadge from '@/components/StatusBadge.vue'
import { timeAgo } from '@/utils/format'

defineProps<{ documents: DocumentSummary[]; showOwner?: boolean }>()
</script>

<template>
  <div class="overflow-x-auto">
    <table class="min-w-full divide-y divide-slate-200">
      <thead class="bg-slate-50">
        <tr>
          <th class="table-head">Document</th>
          <th class="table-head">Status</th>
          <th v-if="showOwner" class="table-head">Owner</th>
          <th class="table-head">Waiting on</th>
          <th class="table-head text-right">Last activity</th>
        </tr>
      </thead>
      <tbody class="divide-y divide-slate-100 bg-white">
        <tr v-for="d in documents" :key="d.id" class="hover:bg-slate-50">
          <td class="table-cell">
            <RouterLink :to="`/documents/${d.id}`" class="font-medium text-slate-900 hover:text-brand-700">{{ d.title }}</RouterLink>
            <p class="text-xs text-slate-500">{{ d.category }} · v{{ d.versionCount }}</p>
          </td>
          <td class="table-cell"><StatusBadge :status="d.status" /></td>
          <td v-if="showOwner" class="table-cell text-slate-600">{{ d.owner.fullName }}</td>
          <td class="table-cell text-slate-600">{{ d.currentApprover?.fullName ?? '—' }}</td>
          <td class="table-cell text-right text-slate-500">{{ timeAgo(d.lastActivityAt) }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
