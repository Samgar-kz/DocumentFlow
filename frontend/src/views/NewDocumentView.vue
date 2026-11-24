<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { documentsApi } from '@/api'
import { ApiError } from '@/api/http'
import { CATEGORIES, type DocumentCategory } from '@/api/types'
import FileDropzone from '@/components/FileDropzone.vue'
import { useToastStore } from '@/stores/toast'

const router = useRouter()
const toast = useToastStore()

const form = reactive({ title: '', description: '', category: 'Contract' as DocumentCategory })
const file = ref<File | null>(null)
const error = ref<ApiError | null>(null)
const saving = ref(false)

async function submit() {
  if (!file.value) {
    error.value = new ApiError(400, 'Choose a file to upload.', { file: ['Choose a file to upload.'] })
    return
  }
  saving.value = true
  error.value = null
  try {
    const document = await documentsApi.create({ ...form, file: file.value })
    toast.success('Document created as a draft')
    await router.push(`/documents/${document.id}`)
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section class="card max-w-2xl p-6">
    <h2 class="font-semibold text-slate-900">Upload a document</h2>
    <p class="mt-1 text-sm text-slate-500">It is saved as a draft. You choose the approvers when you submit it.</p>

    <form class="mt-6 space-y-5" novalidate @submit.prevent="submit">
      <div>
        <label class="label" for="title">Title</label>
        <input id="title" v-model="form.title" class="input" maxlength="150" placeholder="e.g. Vendor Services Agreement" required />
        <p v-if="error?.field('title')" class="field-error">{{ error.field('title') }}</p>
      </div>
      <div>
        <label class="label" for="category">Category</label>
        <select id="category" v-model="form.category" class="input">
          <option v-for="c in CATEGORIES" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div>
        <label class="label" for="description">Description <span class="font-normal text-slate-400">(optional)</span></label>
        <textarea id="description" v-model="form.description" class="input" rows="3" maxlength="2000" />
      </div>
      <div>
        <span class="label">File</span>
        <FileDropzone v-model="file" :error="error?.field('file')" />
      </div>
      <div class="flex justify-end gap-2">
        <button type="button" class="btn-secondary" @click="router.back()">Cancel</button>
        <button type="submit" class="btn-primary" :disabled="saving">{{ saving ? 'Uploading…' : 'Create draft' }}</button>
      </div>
    </form>
  </section>
</template>
