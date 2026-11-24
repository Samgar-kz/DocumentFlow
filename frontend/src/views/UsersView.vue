<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { adminApi } from '@/api'
import type { AdminUser, UserRole } from '@/api/types'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'
import { formatDate } from '@/utils/format'

const auth = useAuthStore()
const toast = useToastStore()
const users = ref<AdminUser[]>([])

async function load() {
  users.value = await adminApi.users()
}

async function changeRole(user: AdminUser, role: UserRole) {
  try {
    await adminApi.changeRole(user.id, role)
    toast.success(`${user.fullName} is now ${role === 'Admin' ? 'an admin' : 'a member'}`)
  } catch (err) {
    toast.error(err)
  }
  await load()
}

onMounted(load)
</script>

<template>
  <section class="card overflow-hidden">
    <div class="overflow-x-auto">
      <table class="min-w-full divide-y divide-slate-200">
        <thead class="bg-slate-50">
          <tr>
            <th class="table-head">Name</th>
            <th class="table-head">Email</th>
            <th class="table-head">Department</th>
            <th class="table-head">Joined</th>
            <th class="table-head">Last sign-in</th>
            <th class="table-head">Role</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          <tr v-for="user in users" :key="user.id">
            <td class="table-cell font-medium text-slate-900">{{ user.fullName }}</td>
            <td class="table-cell text-slate-600">{{ user.email }}</td>
            <td class="table-cell text-slate-600">{{ user.department }}</td>
            <td class="table-cell text-slate-500">{{ formatDate(user.createdAt) }}</td>
            <td class="table-cell text-slate-500">{{ user.lastLoginAt ? formatDate(user.lastLoginAt) : '—' }}</td>
            <td class="table-cell">
              <select
                class="input w-32 py-1"
                :value="user.role"
                :disabled="user.id === auth.user?.id"
                :aria-label="`Role of ${user.fullName}`"
                @change="(e) => changeRole(user, (e.target as HTMLSelectElement).value as UserRole)"
              >
                <option value="Member">Member</option>
                <option value="Admin">Admin</option>
              </select>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
