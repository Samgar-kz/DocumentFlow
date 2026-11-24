<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { workspaceApi } from '@/api'
import type { Notification, NotificationFeed } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'
import { timeAgo } from '@/utils/format'

const router = useRouter()
const feed = ref<NotificationFeed>({ unreadCount: 0, items: [] })
const open = ref(false)
let timer: ReturnType<typeof setInterval> | undefined

async function load() {
  try {
    feed.value = await workspaceApi.notifications()
  } catch {
    // Polling is best-effort; the next tick retries.
  }
}

async function openNotification(notification: Notification) {
  open.value = false
  if (!notification.isRead) {
    await workspaceApi.markRead(notification.id)
  }
  await router.push(`/documents/${notification.documentId}`)
  await load()
}

async function markAll() {
  await workspaceApi.markAllRead()
  await load()
}

onMounted(() => {
  void load()
  timer = setInterval(load, 30_000)
})
onBeforeUnmount(() => clearInterval(timer))
</script>

<template>
  <div class="relative">
    <button class="btn-ghost relative" aria-label="Notifications" @click="open = !open">
      <AppIcon name="bell" />
      <span
        v-if="feed.unreadCount > 0"
        class="absolute -top-0.5 -right-0.5 grid min-w-5 place-items-center rounded-full bg-rose-600 px-1 text-[11px] font-semibold text-white"
      >
        {{ feed.unreadCount > 9 ? '9+' : feed.unreadCount }}
      </span>
    </button>

    <div v-if="open" class="fixed inset-0 z-30" @click="open = false" />
    <div v-if="open" class="card absolute right-0 z-40 mt-2 w-96 overflow-hidden">
      <div class="flex items-center justify-between border-b border-slate-100 px-4 py-3">
        <p class="font-semibold text-slate-900">Notifications</p>
        <button v-if="feed.unreadCount" class="text-sm font-medium text-brand-600 hover:text-brand-700" @click="markAll">
          Mark all read
        </button>
      </div>
      <ul class="max-h-96 divide-y divide-slate-100 overflow-y-auto">
        <li v-for="n in feed.items" :key="n.id">
          <button class="flex w-full gap-3 px-4 py-3 text-left hover:bg-slate-50" @click="openNotification(n)">
            <span class="mt-1.5 size-2 shrink-0 rounded-full" :class="n.isRead ? 'bg-transparent' : 'bg-brand-500'" />
            <span class="min-w-0">
              <span class="block text-sm text-slate-800">{{ n.message }}</span>
              <span class="text-xs text-slate-500">{{ timeAgo(n.createdAt) }}</span>
            </span>
          </button>
        </li>
        <li v-if="feed.items.length === 0" class="px-4 py-8 text-center text-sm text-slate-500">You're all caught up.</li>
      </ul>
    </div>
  </div>
</template>
