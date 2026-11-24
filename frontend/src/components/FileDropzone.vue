<script setup lang="ts">
import { ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { fileSize } from '@/utils/format'

const model = defineModel<File | null>({ required: true })
defineProps<{ error?: string }>()

const ACCEPT = '.pdf,.docx,.xlsx,.pptx,.png,.jpg,.jpeg,.txt,.csv'
const dragging = ref(false)
const input = ref<HTMLInputElement | null>(null)

function pick(files: FileList | null | undefined) {
  if (files && files.length > 0) model.value = files[0] ?? null
}
</script>

<template>
  <div>
    <div
      class="flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed px-6 py-8 text-center transition"
      :class="dragging ? 'border-brand-500 bg-brand-50' : error ? 'border-rose-300' : 'border-slate-300 hover:border-brand-500'"
      role="button"
      tabindex="0"
      @click="input?.click()"
      @keydown.enter="input?.click()"
      @dragover.prevent="dragging = true"
      @dragleave.prevent="dragging = false"
      @drop.prevent="(e) => { dragging = false; pick(e.dataTransfer?.files) }"
    >
      <AppIcon name="upload" :size="28" class="text-slate-400" />
      <template v-if="model">
        <p class="mt-2 font-medium text-slate-900">{{ model.name }}</p>
        <p class="text-xs text-slate-500">{{ fileSize(model.size) }} · click to replace</p>
      </template>
      <template v-else>
        <p class="mt-2 text-sm text-slate-700"><span class="font-medium text-brand-600">Choose a file</span> or drag it here</p>
        <p class="text-xs text-slate-500">PDF, Word, Excel, PowerPoint, images, TXT or CSV · up to 10 MB</p>
      </template>
      <input ref="input" type="file" class="sr-only" :accept="ACCEPT" aria-label="File" @change="(e) => pick((e.target as HTMLInputElement).files)" />
    </div>
    <p v-if="error" class="field-error">{{ error }}</p>
  </div>
</template>
