<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { workspaceApi } from '@/api'
import type { DirectoryUser } from '@/api/types'
import AppIcon from '@/components/AppIcon.vue'

const props = defineProps<{ excludeId: string; max?: number }>()
const route = defineModel<DirectoryUser[]>({ required: true })

const search = ref('')
const people = ref<DirectoryUser[]>([])
let timer: ReturnType<typeof setTimeout> | undefined

const limit = computed(() => props.max ?? 5)
const candidates = computed(() =>
  people.value.filter((p) => p.id !== props.excludeId && !route.value.some((r) => r.id === p.id)).slice(0, 6),
)

async function load() {
  people.value = await workspaceApi.directory(search.value || undefined)
}

function add(person: DirectoryUser) {
  if (route.value.length < limit.value) route.value = [...route.value, person]
}

function move(index: number, delta: number) {
  const next = [...route.value]
  const [item] = next.splice(index, 1)
  next.splice(index + delta, 0, item!)
  route.value = next
}

function remove(index: number) {
  route.value = route.value.filter((_, i) => i !== index)
}

watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(load, 250)
})
onMounted(load)
</script>

<template>
  <div class="space-y-4">
    <div>
      <p class="label">Approval route <span class="font-normal text-slate-400">(reviewed in this order)</span></p>
      <ol v-if="route.length" class="divide-y divide-slate-100 rounded-lg border border-slate-200">
        <li v-for="(person, index) in route" :key="person.id" class="flex items-center gap-3 px-3 py-2">
          <span class="grid size-6 place-items-center rounded-full bg-brand-100 text-xs font-semibold text-brand-700">{{ index + 1 }}</span>
          <span class="flex-1 text-sm">
            <span class="font-medium text-slate-900">{{ person.fullName }}</span>
            <span class="text-slate-500"> · {{ person.department }}</span>
          </span>
          <button type="button" class="btn-ghost px-1" :disabled="index === 0" aria-label="Move up" @click="move(index, -1)">
            <AppIcon name="up" :size="16" />
          </button>
          <button type="button" class="btn-ghost px-1" :disabled="index === route.length - 1" aria-label="Move down" @click="move(index, 1)">
            <AppIcon name="down" :size="16" />
          </button>
          <button type="button" class="btn-ghost px-1 hover:text-rose-600" aria-label="Remove" @click="remove(index)">
            <AppIcon name="close" :size="16" />
          </button>
        </li>
      </ol>
      <p v-else class="rounded-lg border border-dashed border-slate-300 px-3 py-4 text-center text-sm text-slate-500">
        Add at least one approver.
      </p>
    </div>

    <div v-if="route.length < limit">
      <label class="label" for="people-search">Add approver</label>
      <input id="people-search" v-model="search" class="input" type="search" placeholder="Search by name, email or department" />
      <ul class="mt-2 divide-y divide-slate-100 rounded-lg border border-slate-200">
        <li v-for="person in candidates" :key="person.id">
          <button type="button" class="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-slate-50" @click="add(person)">
            <span>
              <span class="font-medium text-slate-900">{{ person.fullName }}</span>
              <span class="text-slate-500"> · {{ person.department }}</span>
            </span>
            <AppIcon name="plus" :size="16" class="text-brand-600" />
          </button>
        </li>
        <li v-if="candidates.length === 0" class="px-3 py-2 text-sm text-slate-500">No matching people.</li>
      </ul>
    </div>
  </div>
</template>
