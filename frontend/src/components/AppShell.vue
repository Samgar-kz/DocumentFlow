<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import AppIcon, { type IconName } from '@/components/AppIcon.vue'
import NotificationBell from '@/components/NotificationBell.vue'
import { useAuthStore } from '@/stores/auth'
import { initials } from '@/utils/format'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const mobileOpen = ref(false)

const links = computed(() => {
  const items: { to: string; label: string; icon: IconName; exact?: boolean }[] = [
    { to: '/', label: 'Dashboard', icon: 'home', exact: true },
    { to: '/documents', label: 'My documents', icon: 'document' },
    { to: '/approvals', label: 'Approvals', icon: 'inbox' },
    { to: '/shared', label: 'Shared with me', icon: 'users' },
  ]
  if (auth.isAdmin) {
    items.push({ to: '/admin/audit', label: 'Audit log', icon: 'history' }, { to: '/admin/users', label: 'Users & roles', icon: 'shield' })
  }
  return items
})

watch(() => route.fullPath, () => (mobileOpen.value = false))

async function logout() {
  await auth.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <div class="min-h-screen lg:flex">
    <aside
      class="fixed inset-y-0 left-0 z-40 flex w-64 flex-col bg-slate-900 text-slate-300 transition-transform lg:translate-x-0"
      :class="mobileOpen ? 'translate-x-0' : '-translate-x-full'"
    >
      <div class="flex h-16 items-center gap-2 px-6">
        <span class="grid size-8 place-items-center rounded-lg bg-brand-500 text-white"><AppIcon name="document" :size="18" /></span>
        <span class="text-lg font-semibold text-white">DocumentFlow</span>
      </div>

      <div class="px-3 pt-2">
        <RouterLink to="/documents/new" class="btn-primary w-full"><AppIcon name="plus" :size="16" /> New document</RouterLink>
      </div>

      <nav class="flex-1 space-y-1 px-3 py-4" aria-label="Main">
        <RouterLink
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium hover:bg-slate-800 hover:text-white"
          :active-class="link.exact ? '' : 'bg-slate-800 text-white'"
          :exact-active-class="link.exact ? 'bg-slate-800 text-white' : ''"
        >
          <AppIcon :name="link.icon" />
          {{ link.label }}
        </RouterLink>
      </nav>

      <div class="border-t border-slate-800 p-4">
        <div class="flex items-center gap-3">
          <span class="grid size-9 place-items-center rounded-full bg-slate-700 text-sm font-semibold text-white">
            {{ initials(auth.user?.fullName ?? '?') }}
          </span>
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-medium text-white">{{ auth.user?.fullName }}</p>
            <p class="truncate text-xs text-slate-400">{{ auth.user?.department }}</p>
          </div>
          <button class="rounded-md p-1.5 hover:bg-slate-800 hover:text-white" title="Sign out" @click="logout">
            <AppIcon name="logout" />
            <span class="sr-only">Sign out</span>
          </button>
        </div>
      </div>
    </aside>

    <div v-if="mobileOpen" class="fixed inset-0 z-30 bg-slate-900/50 lg:hidden" @click="mobileOpen = false" />

    <div class="flex min-h-screen flex-1 flex-col lg:pl-64">
      <header class="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-slate-200 bg-white/90 px-4 backdrop-blur lg:px-8">
        <button class="btn-ghost lg:hidden" aria-label="Open menu" @click="mobileOpen = true">
          <AppIcon name="menu" />
        </button>
        <h1 class="flex-1 text-lg font-semibold text-slate-900">{{ route.meta.title }}</h1>
        <NotificationBell />
      </header>
      <main class="mx-auto w-full max-w-7xl flex-1 p-4 lg:p-8">
        <slot />
      </main>
    </div>
  </div>
</template>
