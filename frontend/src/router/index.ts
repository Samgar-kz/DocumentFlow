import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    admin?: boolean
    title?: string
  }
}

const DocumentsView = () => import('@/views/DocumentsView.vue')

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue'), meta: { public: true, title: 'Sign in' } },
    { path: '/register', name: 'register', component: () => import('@/views/RegisterView.vue'), meta: { public: true, title: 'Create account' } },
    { path: '/', name: 'dashboard', component: () => import('@/views/DashboardView.vue'), meta: { title: 'Dashboard' } },
    { path: '/documents', name: 'documents', component: DocumentsView, props: { scope: 'Mine' }, meta: { title: 'My documents' } },
    { path: '/approvals', name: 'approvals', component: DocumentsView, props: { scope: 'AwaitingMe' }, meta: { title: 'Awaiting my approval' } },
    { path: '/shared', name: 'shared', component: DocumentsView, props: { scope: 'Involved' }, meta: { title: 'Shared with me' } },
    { path: '/documents/new', name: 'new-document', component: () => import('@/views/NewDocumentView.vue'), meta: { title: 'New document' } },
    { path: '/documents/:id', name: 'document', component: () => import('@/views/DocumentDetailView.vue'), props: true, meta: { title: 'Document' } },
    { path: '/admin/audit', name: 'audit', component: () => import('@/views/AuditLogView.vue'), meta: { admin: true, title: 'Audit log' } },
    { path: '/admin/users', name: 'users', component: () => import('@/views/UsersView.vue'), meta: { admin: true, title: 'Users & roles' } },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue'), meta: { public: true, title: 'Not found' } },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.restore()

  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: to.fullPath !== '/' ? { redirect: to.fullPath } : {} }
  }
  if ((to.name === 'login' || to.name === 'register') && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }
  if (to.meta.admin && !auth.isAdmin) {
    return { name: 'dashboard' }
  }
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · DocumentFlow` : 'DocumentFlow'
})

export default router
