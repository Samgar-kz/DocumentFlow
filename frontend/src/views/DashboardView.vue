<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { workspaceApi } from '@/api'
import type { Dashboard } from '@/api/types'
import ActivityFeed from '@/components/ActivityFeed.vue'
import DocumentTable from '@/components/DocumentTable.vue'
import StatCard from '@/components/StatCard.vue'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'

const auth = useAuthStore()
const toast = useToastStore()
const dashboard = ref<Dashboard | null>(null)

onMounted(async () => {
  try {
    dashboard.value = await workspaceApi.dashboard()
  } catch (err) {
    toast.error(err)
  }
})
</script>

<template>
  <div class="space-y-6">
    <div>
      <p class="text-sm text-slate-500">{{ auth.user?.department }}</p>
      <h2 class="text-2xl font-semibold text-slate-900">Welcome back, {{ auth.user?.fullName.split(' ')[0] }}</h2>
    </div>

    <div v-if="dashboard" class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard label="Awaiting my approval" :value="String(dashboard.awaitingMyApproval)" :tone="dashboard.awaitingMyApproval ? 'negative' : 'neutral'" hint="Documents where it's your turn" />
      <StatCard label="My drafts" :value="String(dashboard.myDrafts)" hint="Not yet submitted" />
      <StatCard label="My documents in review" :value="String(dashboard.myInReview)" />
      <StatCard label="Approved (30 days)" :value="String(dashboard.myApprovedLast30Days)" tone="positive" />
    </div>

    <div v-if="dashboard" class="grid gap-6 xl:grid-cols-3">
      <div class="space-y-6 xl:col-span-2">
        <section class="card overflow-hidden">
          <div class="flex items-center justify-between p-5 pb-3">
            <h3 class="font-semibold text-slate-900">Waiting for your approval</h3>
            <RouterLink to="/approvals" class="text-sm font-medium text-brand-600 hover:text-brand-700">View all</RouterLink>
          </div>
          <DocumentTable v-if="dashboard.awaiting.length" :documents="dashboard.awaiting" show-owner />
          <p v-else class="px-5 pb-5 text-sm text-slate-500">Nothing needs your review right now.</p>
        </section>

        <section class="card overflow-hidden">
          <div class="flex items-center justify-between p-5 pb-3">
            <h3 class="font-semibold text-slate-900">My recent documents</h3>
            <RouterLink to="/documents" class="text-sm font-medium text-brand-600 hover:text-brand-700">View all</RouterLink>
          </div>
          <DocumentTable v-if="dashboard.recentlyUpdated.length" :documents="dashboard.recentlyUpdated" />
          <p v-else class="px-5 pb-5 text-sm text-slate-500">
            You haven't created any documents yet.
            <RouterLink to="/documents/new" class="font-medium text-brand-600">Upload one</RouterLink>
          </p>
        </section>
      </div>

      <section class="card p-5">
        <h3 class="mb-4 font-semibold text-slate-900">Recent activity</h3>
        <ActivityFeed :items="dashboard.activity" show-document />
      </section>
    </div>
  </div>
</template>
