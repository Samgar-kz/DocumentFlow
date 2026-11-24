<script setup lang="ts">
import AppIcon from '@/components/AppIcon.vue'

defineProps<{ title: string; subtitle: string }>()
</script>

<template>
  <div class="grid min-h-screen lg:grid-cols-2">
    <section class="hidden flex-col justify-between bg-slate-900 p-12 text-white lg:flex">
      <div class="flex items-center gap-2">
        <span class="grid size-9 place-items-center rounded-lg bg-brand-500"><AppIcon name="document" /></span>
        <span class="text-xl font-semibold">DocumentFlow</span>
      </div>
      <div class="max-w-md">
        <h2 class="text-3xl leading-tight font-semibold">Documents, approvals and audit trail in one place.</h2>
        <p class="mt-4 text-slate-400">
          Upload contracts, invoices and policies, route them to the right people and keep a complete record of every
          decision.
        </p>
        <ul class="mt-8 space-y-3 text-sm text-slate-300">
          <li>✓ Sequential, multi-step approval routes</li>
          <li>✓ Versioned files with SHA-256 integrity hashes</li>
          <li>✓ Tamper-evident audit trail of every action</li>
          <li>✓ In-app notifications for decisions and comments</li>
        </ul>
      </div>
      <p class="text-xs text-slate-500">ASP.NET Core · PostgreSQL · Vue 3 · TypeScript</p>
    </section>

    <section class="flex items-center justify-center p-6">
      <div class="w-full max-w-sm">
        <h1 class="text-2xl font-semibold text-slate-900">{{ title }}</h1>
        <p class="mt-1 text-sm text-slate-500">{{ subtitle }}</p>
        <div class="mt-8">
          <slot />
        </div>
      </div>
    </section>
  </div>
</template>
