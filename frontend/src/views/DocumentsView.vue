<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { documentsApi } from '@/api'
import { CATEGORIES, STATUSES, type DocumentQuery, type DocumentScope, type DocumentSummary, type PagedResult } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'
import DocumentTable from '@/components/DocumentTable.vue'
import EmptyState from '@/components/EmptyState.vue'
import PaginationBar from '@/components/PaginationBar.vue'
import { useToastStore } from '@/stores/toast'
import { statusLabel } from '@/utils/format'

const props = defineProps<{ scope: DocumentScope }>()
const toast = useToastStore()

const filters = reactive<DocumentQuery>({ search: '', page: 1, pageSize: 20 })
const result = ref<PagedResult<DocumentSummary> | null>(null)
const loading = ref(false)
let timer: ReturnType<typeof setTimeout> | undefined

const empty: Record<DocumentScope, { title: string; description: string }> = {
  Mine: { title: 'No documents yet', description: 'Upload a contract, invoice or policy to start an approval.' },
  AwaitingMe: { title: 'Inbox zero', description: 'No documents are waiting for your decision.' },
  Involved: { title: 'Nothing shared with you', description: 'Documents appear here when someone adds you as an approver.' },
  All: { title: 'No documents', description: '' },
}

async function load() {
  loading.value = true
  try {
    result.value = await documentsApi.search({ ...filters, scope: props.scope })
  } catch (err) {
    toast.error(err)
  } finally {
    loading.value = false
  }
}

watch(
  () => [props.scope, filters.status, filters.category],
  () => {
    filters.page = 1
    void load()
  },
  { immediate: true },
)

watch(
  () => filters.search,
  () => {
    clearTimeout(timer)
    timer = setTimeout(() => {
      filters.page = 1
      void load()
    }, 300)
  },
)

function changePage(page: number) {
  filters.page = page
  void load()
}
</script>

<template>
  <div class="space-y-4">
    <div class="card grid gap-3 p-4 sm:grid-cols-4">
      <input v-model="filters.search" class="input sm:col-span-2" type="search" placeholder="Search by title" aria-label="Search" />
      <select v-model="filters.status" class="input" aria-label="Status">
        <option :value="undefined">All statuses</option>
        <option v-for="s in STATUSES" :key="s" :value="s">{{ statusLabel[s] }}</option>
      </select>
      <select v-model="filters.category" class="input" aria-label="Category">
        <option :value="undefined">All categories</option>
        <option v-for="c in CATEGORIES" :key="c" :value="c">{{ c }}</option>
      </select>
    </div>

    <section class="card overflow-hidden">
      <DocumentTable v-if="result?.items.length" :documents="result.items" :show-owner="scope !== 'Mine'" />
      <div v-else-if="!loading" class="p-6">
        <EmptyState :title="empty[scope].title" :description="empty[scope].description">
          <RouterLink v-if="scope === 'Mine'" to="/documents/new" class="btn-primary"><AppIcon name="plus" :size="16" /> New document</RouterLink>
        </EmptyState>
      </div>
      <PaginationBar
        v-if="result && result.totalCount > 0"
        :page="result.page"
        :total-pages="result.totalPages"
        :total-count="result.totalCount"
        @change="changePage"
      />
    </section>
  </div>
</template>
