<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { adminApi } from '@/api'
import type { Activity, AuditAction, AuditQuery, PagedResult } from '@/api/types'
import PaginationBar from '@/components/PaginationBar.vue'
import { useToastStore } from '@/stores/toast'
import { actionLabel, formatDateTime } from '@/utils/format'

const toast = useToastStore()
const query = reactive<AuditQuery>({ page: 1, pageSize: 25 })
const result = ref<PagedResult<Activity> | null>(null)
const actions = Object.keys(actionLabel) as AuditAction[]

async function load() {
  try {
    result.value = await adminApi.audit(query)
  } catch (err) {
    toast.error(err)
  }
}

watch(
  () => [query.action, query.from, query.to],
  () => {
    query.page = 1
    void load()
  },
  { immediate: true },
)

function changePage(page: number) {
  query.page = page
  void load()
}
</script>

<template>
  <div class="space-y-4">
    <div class="card grid gap-3 p-4 sm:grid-cols-4">
      <select v-model="query.action" class="input" aria-label="Action">
        <option :value="undefined">All actions</option>
        <option v-for="a in actions" :key="a" :value="a">{{ actionLabel[a] }}</option>
      </select>
      <input v-model="query.from" class="input" type="date" aria-label="From" />
      <input v-model="query.to" class="input" type="date" aria-label="To" />
      <p class="self-center text-xs text-slate-500">Append-only record of every action, kept even after drafts are deleted.</p>
    </div>

    <section class="card overflow-hidden">
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-slate-200">
          <thead class="bg-slate-50">
            <tr>
              <th class="table-head">When</th>
              <th class="table-head">Who</th>
              <th class="table-head">Action</th>
              <th class="table-head">Document</th>
              <th class="table-head">Details</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100">
            <tr v-for="entry in result?.items" :key="entry.id">
              <td class="table-cell text-slate-500">{{ formatDateTime(entry.occurredAt) }}</td>
              <td class="table-cell font-medium text-slate-900">{{ entry.actor?.fullName ?? 'Unknown' }}</td>
              <td class="table-cell">{{ actionLabel[entry.action] }}</td>
              <td class="table-cell max-w-56 truncate">
                <RouterLink v-if="entry.documentId && entry.action !== 'Deleted'" :to="`/documents/${entry.documentId}`" class="text-brand-700 hover:underline">
                  {{ entry.documentTitle }}
                </RouterLink>
                <span v-else class="text-slate-600">{{ entry.documentTitle ?? '—' }}</span>
              </td>
              <td class="table-cell max-w-md truncate text-slate-600" :title="entry.details">{{ entry.details }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <PaginationBar v-if="result" :page="result.page" :total-pages="result.totalPages" :total-count="result.totalCount" @change="changePage" />
    </section>
  </div>
</template>
