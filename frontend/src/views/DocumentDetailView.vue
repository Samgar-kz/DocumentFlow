<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { documentsApi } from '@/api'
import { ApiError } from '@/api/http'
import { CATEGORIES, type Activity, type DirectoryUser, type DocumentCategory, type DocumentDetail, type StepStatus } from '@/api/types'
import ActivityFeed from '@/components/ActivityFeed.vue'
import AppIcon from '@/components/AppIcon.vue'
import ApproverPicker from '@/components/ApproverPicker.vue'
import BaseModal from '@/components/BaseModal.vue'
import FileDropzone from '@/components/FileDropzone.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'
import { fileSize, formatDate, formatDateTime, initials, timeAgo } from '@/utils/format'

const props = defineProps<{ id: string }>()
const router = useRouter()
const auth = useAuthStore()
const toast = useToastStore()

const doc = ref<DocumentDetail | null>(null)
const activity = ref<Activity[]>([])
const busy = ref(false)
const modal = ref<'submit' | 'approve' | 'reject' | 'upload' | 'edit' | null>(null)
const error = ref<ApiError | null>(null)

const route = ref<DirectoryUser[]>([])
const decisionComment = ref('')
const upload = reactive({ file: null as File | null, comment: '' })
const edit = reactive({ title: '', description: '', category: 'Contract' as DocumentCategory })
const newComment = ref('')

const rejectedStep = computed(() => doc.value?.steps.find((s) => s.status === 'Rejected'))
const stepStyle: Record<StepStatus, string> = {
  Pending: 'bg-white text-slate-500 ring-slate-300',
  Approved: 'bg-emerald-500 text-white ring-emerald-500',
  Rejected: 'bg-rose-500 text-white ring-rose-500',
  Skipped: 'bg-slate-100 text-slate-400 ring-slate-200',
}

async function load() {
  try {
    const [detail, trail] = await Promise.all([documentsApi.get(props.id), documentsApi.activity(props.id)])
    doc.value = detail
    activity.value = trail
  } catch (err) {
    toast.error(err)
    if (err instanceof ApiError && err.status === 404) await router.replace('/documents')
  }
}

async function run(action: () => Promise<DocumentDetail | void>, success: string) {
  busy.value = true
  error.value = null
  try {
    const updated = await action()
    if (updated) doc.value = updated
    activity.value = await documentsApi.activity(props.id)
    modal.value = null
    toast.success(success)
  } catch (err) {
    if (err instanceof ApiError) error.value = err
    toast.error(err)
  } finally {
    busy.value = false
  }
}

function open(kind: NonNullable<typeof modal.value>) {
  error.value = null
  decisionComment.value = ''
  if (kind === 'submit') route.value = []
  if (kind === 'upload') Object.assign(upload, { file: null, comment: '' })
  if (kind === 'edit' && doc.value) {
    Object.assign(edit, { title: doc.value.title, description: doc.value.description ?? '', category: doc.value.category })
  }
  modal.value = kind
}

const submit = () => run(() => documentsApi.submit(props.id, route.value.map((p) => p.id)), 'Submitted for approval')
const approve = () => run(() => documentsApi.approve(props.id, decisionComment.value || null), 'Approved')
const reject = () => run(() => documentsApi.reject(props.id, decisionComment.value), 'Rejected')
const withdraw = () => run(() => documentsApi.withdraw(props.id), 'Withdrawn from review')
const saveEdit = () => run(() => documentsApi.update(props.id, { ...edit, description: edit.description || null }), 'Details saved')

function uploadVersion() {
  if (!upload.file) {
    error.value = new ApiError(400, 'Choose a file.', { file: ['Choose a file.'] })
    return
  }
  const file = upload.file
  void run(() => documentsApi.uploadVersion(props.id, file, upload.comment), 'New version uploaded')
}

async function addComment() {
  if (!newComment.value.trim()) return
  await run(() => documentsApi.comment(props.id, newComment.value), 'Comment added')
  if (!error.value) newComment.value = ''
}

async function remove() {
  if (!window.confirm('Delete this draft and all of its files? The audit trail is kept.')) return
  try {
    await documentsApi.remove(props.id)
    toast.success('Draft deleted')
    await router.push('/documents')
  } catch (err) {
    toast.error(err)
  }
}

async function download(number: number, fileName: string) {
  try {
    await documentsApi.download(props.id, number, fileName)
    activity.value = await documentsApi.activity(props.id)
  } catch (err) {
    toast.error(err)
  }
}

watch(() => props.id, load, { immediate: true })
</script>

<template>
  <div v-if="doc" class="space-y-6">
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <RouterLink to="/documents" class="text-sm text-slate-500 hover:text-slate-700">← Documents</RouterLink>
        <div class="mt-1 flex flex-wrap items-center gap-3">
          <h2 class="text-2xl font-semibold text-slate-900">{{ doc.title }}</h2>
          <StatusBadge :status="doc.status" />
        </div>
        <p class="mt-1 text-sm text-slate-500">
          {{ doc.category }} · Owned by {{ doc.owner.fullName }} ({{ doc.owner.department }}) · Created {{ formatDate(doc.createdAt) }}
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <button v-if="doc.permissions.canEdit" class="btn-secondary" @click="open('edit')"><AppIcon name="pencil" :size="16" /> Edit</button>
        <button v-if="doc.permissions.canUploadVersion" class="btn-secondary" @click="open('upload')"><AppIcon name="upload" :size="16" /> New version</button>
        <button v-if="doc.permissions.canWithdraw" class="btn-secondary" :disabled="busy" @click="withdraw">Withdraw</button>
        <button v-if="doc.permissions.canDelete" class="btn-secondary hover:text-rose-600" @click="remove"><AppIcon name="trash" :size="16" /> Delete</button>
        <button v-if="doc.permissions.canSubmit" class="btn-primary" @click="open('submit')">Submit for approval</button>
        <template v-if="doc.permissions.canDecide">
          <button class="btn-danger" @click="open('reject')">Reject</button>
          <button class="btn-primary" @click="open('approve')"><AppIcon name="check" :size="16" /> Approve</button>
        </template>
      </div>
    </div>

    <div v-if="doc.permissions.canDecide" class="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
      <strong>It's your turn.</strong> Review the latest version and approve or reject it.
    </div>
    <div v-if="doc.status === 'Rejected' && rejectedStep" class="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900">
      <strong>Rejected by {{ rejectedStep.approver.fullName }}:</strong> {{ rejectedStep.comment }}
      <span v-if="doc.permissions.canUploadVersion"> Upload a revised version to resubmit.</span>
    </div>

    <div class="grid gap-6 xl:grid-cols-3">
      <div class="space-y-6 xl:col-span-2">
        <section v-if="doc.description" class="card p-5">
          <h3 class="mb-2 font-semibold text-slate-900">Description</h3>
          <p class="text-sm whitespace-pre-line text-slate-700">{{ doc.description }}</p>
        </section>

        <section class="card p-5">
          <h3 class="mb-4 font-semibold text-slate-900">Approval route</h3>
          <ol v-if="doc.steps.length" class="space-y-4">
            <li v-for="step in doc.steps" :key="step.order" class="flex gap-3">
              <span class="grid size-8 shrink-0 place-items-center rounded-full text-xs font-semibold ring-2" :class="stepStyle[step.status]">
                <AppIcon v-if="step.status === 'Approved'" name="check" :size="16" />
                <AppIcon v-else-if="step.status === 'Rejected'" name="close" :size="16" />
                <template v-else>{{ step.order }}</template>
              </span>
              <div class="text-sm">
                <p class="font-medium text-slate-900">
                  {{ step.approver.fullName }} <span class="font-normal text-slate-500">· {{ step.approver.department }}</span>
                </p>
                <p v-if="step.isCurrent" class="flex items-center gap-1 text-amber-700"><AppIcon name="clock" :size="14" /> Waiting for decision</p>
                <p v-else-if="step.decidedAt" class="text-slate-500">{{ step.status }} {{ timeAgo(step.decidedAt) }}</p>
                <p v-else class="text-slate-400">{{ step.status }}</p>
                <p v-if="step.comment" class="mt-1 rounded-lg bg-slate-50 px-3 py-2 text-slate-700">“{{ step.comment }}”</p>
              </div>
            </li>
          </ol>
          <p v-else class="text-sm text-slate-500">Not submitted yet. The owner chooses the approvers when submitting.</p>
        </section>

        <section class="card overflow-hidden">
          <h3 class="p-5 pb-3 font-semibold text-slate-900">Versions</h3>
          <table class="min-w-full divide-y divide-slate-200">
            <thead class="bg-slate-50">
              <tr>
                <th class="table-head">Version</th>
                <th class="table-head">File</th>
                <th class="table-head">Uploaded</th>
                <th class="table-head">SHA-256</th>
                <th class="table-head"><span class="sr-only">Download</span></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              <tr v-for="v in doc.versions" :key="v.number">
                <td class="table-cell font-medium">v{{ v.number }}</td>
                <td class="table-cell">
                  <p class="max-w-56 truncate text-slate-900">{{ v.fileName }}</p>
                  <p class="text-xs text-slate-500">{{ fileSize(v.sizeBytes) }}<span v-if="v.comment"> · {{ v.comment }}</span></p>
                </td>
                <td class="table-cell text-slate-600">{{ v.uploadedBy.fullName }}<br /><span class="text-xs text-slate-500">{{ formatDateTime(v.uploadedAt) }}</span></td>
                <td class="table-cell font-mono text-xs text-slate-500" :title="v.sha256">{{ v.sha256.slice(0, 12) }}…</td>
                <td class="table-cell text-right">
                  <button class="btn-ghost" :aria-label="`Download version ${v.number}`" @click="download(v.number, v.fileName)">
                    <AppIcon name="download" :size="16" />
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="card p-5">
          <h3 class="mb-4 font-semibold text-slate-900">Comments</h3>
          <ul class="space-y-4">
            <li v-for="c in doc.comments" :key="c.id" class="flex gap-3">
              <span class="grid size-8 shrink-0 place-items-center rounded-full bg-slate-100 text-xs font-semibold text-slate-600">{{ initials(c.author.fullName) }}</span>
              <div class="text-sm">
                <p><span class="font-medium text-slate-900">{{ c.author.fullName }}</span> <span class="text-xs text-slate-500">{{ timeAgo(c.createdAt) }}</span></p>
                <p class="whitespace-pre-line text-slate-700">{{ c.body }}</p>
              </div>
            </li>
            <li v-if="doc.comments.length === 0" class="text-sm text-slate-500">No comments yet.</li>
          </ul>
          <form v-if="doc.permissions.canComment" class="mt-4 flex gap-2" @submit.prevent="addComment">
            <input v-model="newComment" class="input" maxlength="2000" placeholder="Write a comment…" aria-label="Comment" />
            <button class="btn-primary" type="submit" :disabled="busy || !newComment.trim()"><AppIcon name="chat" :size="16" /> Post</button>
          </form>
        </section>
      </div>

      <section class="card h-fit p-5">
        <h3 class="mb-4 font-semibold text-slate-900">Audit trail</h3>
        <ActivityFeed :items="activity" />
      </section>
    </div>

    <BaseModal v-if="modal === 'submit'" title="Submit for approval" @close="modal = null">
      <ApproverPicker v-model="route" :exclude-id="auth.user?.id ?? ''" />
      <p v-if="error" class="field-error mt-3">{{ error.field('approverIds') ?? error.message }}</p>
      <div class="mt-6 flex justify-end gap-2">
        <button class="btn-secondary" @click="modal = null">Cancel</button>
        <button class="btn-primary" :disabled="busy || route.length === 0" @click="submit">Submit</button>
      </div>
    </BaseModal>

    <BaseModal v-if="modal === 'approve' || modal === 'reject'" :title="modal === 'approve' ? 'Approve document' : 'Reject document'" @close="modal = null">
      <label class="label" for="decision">{{ modal === 'approve' ? 'Comment (optional)' : 'Reason for rejection' }}</label>
      <textarea id="decision" v-model="decisionComment" class="input" rows="3" maxlength="1000" />
      <p v-if="error" class="field-error">{{ error.message }}</p>
      <div class="mt-6 flex justify-end gap-2">
        <button class="btn-secondary" @click="modal = null">Cancel</button>
        <button v-if="modal === 'approve'" class="btn-primary" :disabled="busy" @click="approve">Approve</button>
        <button v-else class="btn-danger" :disabled="busy || !decisionComment.trim()" @click="reject">Reject</button>
      </div>
    </BaseModal>

    <BaseModal v-if="modal === 'upload'" title="Upload new version" @close="modal = null">
      <div class="space-y-4">
        <FileDropzone v-model="upload.file" :error="error?.field('file')" />
        <div>
          <label class="label" for="version-comment">What changed? <span class="font-normal text-slate-400">(optional)</span></label>
          <input id="version-comment" v-model="upload.comment" class="input" maxlength="500" />
        </div>
      </div>
      <div class="mt-6 flex justify-end gap-2">
        <button class="btn-secondary" @click="modal = null">Cancel</button>
        <button class="btn-primary" :disabled="busy" @click="uploadVersion">Upload</button>
      </div>
    </BaseModal>

    <BaseModal v-if="modal === 'edit'" title="Edit details" @close="modal = null">
      <form class="space-y-4" @submit.prevent="saveEdit">
        <div>
          <label class="label" for="edit-title">Title</label>
          <input id="edit-title" v-model="edit.title" class="input" maxlength="150" />
          <p v-if="error?.field('title')" class="field-error">{{ error.field('title') }}</p>
        </div>
        <div>
          <label class="label" for="edit-category">Category</label>
          <select id="edit-category" v-model="edit.category" class="input">
            <option v-for="c in CATEGORIES" :key="c" :value="c">{{ c }}</option>
          </select>
        </div>
        <div>
          <label class="label" for="edit-description">Description</label>
          <textarea id="edit-description" v-model="edit.description" class="input" rows="3" maxlength="2000" />
        </div>
        <div class="flex justify-end gap-2">
          <button type="button" class="btn-secondary" @click="modal = null">Cancel</button>
          <button type="submit" class="btn-primary" :disabled="busy">Save</button>
        </div>
      </form>
    </BaseModal>
  </div>
</template>
