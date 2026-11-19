import type { RouteRecordRaw } from 'vue-router'

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('@/layouts/AppLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'dashboard',
        component: () => import('@/modules/dashboard/pages/DashboardPage.vue'),
        meta: { title: 'Dashboard' },
      },
      {
        path: 'accounts',
        name: 'accounts',
        component: () => import('@/modules/accounts/pages/AccountsPage.vue'),
        meta: { title: 'Contas' },
      },
      {
        path: 'accounts/:id',
        name: 'account-detail',
        component: () => import('@/modules/accounts/pages/AccountDetailPage.vue'),
        meta: { title: 'Conta' },
      },
      {
        path: 'credit-cards',
        name: 'credit-cards',
        component: () => import('@/modules/credit-cards/pages/CreditCardsPage.vue'),
        meta: { title: 'Cartões' },
      },
      {
        path: 'transactions',
        name: 'transactions',
        component: () => import('@/modules/transactions/pages/TransactionsPage.vue'),
        meta: { title: 'Movimentações' },
      },
      {
        path: 'categories',
        name: 'categories',
        component: () => import('@/modules/categories/pages/CategoriesPage.vue'),
        meta: { title: 'Categorias' },
      },
      {
        path: 'installments',
        name: 'installments',
        component: () => import('@/modules/installments/pages/InstallmentsPage.vue'),
        meta: { title: 'Parcelamentos' },
      },
      {
        path: 'recurrences',
        name: 'recurrences',
        component: () => import('@/modules/recurrence/pages/RecurrencesPage.vue'),
        meta: { title: 'Recorrências' },
      },
      {
        path: 'groups',
        name: 'groups',
        component: () => import('@/modules/groups/pages/GroupsPage.vue'),
        meta: { title: 'Grupos' },
      },
      {
        path: 'reports',
        name: 'reports',
        component: () => import('@/modules/reports/pages/ReportsPage.vue'),
        meta: { title: 'Relatórios' },
      },
      {
        path: 'settings',
        name: 'settings',
        component: () => import('@/modules/settings/pages/SettingsPage.vue'),
        meta: { title: 'Configurações' },
      },
    ],
  },
  {
    path: '/',
    component: () => import('@/layouts/AuthLayout.vue'),
    meta: { guestOnly: true },
    children: [
      {
        path: 'login',
        name: 'login',
        component: () => import('@/modules/auth/pages/LoginPage.vue'),
        meta: { title: 'Entrar' },
      },
      {
        path: 'register',
        name: 'register',
        component: () => import('@/modules/auth/pages/RegisterPage.vue'),
        meta: { title: 'Criar conta' },
      },
      {
        path: 'forgot-password',
        name: 'forgot-password',
        component: () => import('@/modules/auth/pages/ForgotPasswordPage.vue'),
        meta: { title: 'Esqueci a senha' },
      },
    ],
  },
  {
    // Fora de `guestOnly`: um link de redefinição deve funcionar mesmo com outra sessão aberta.
    path: '/reset-password',
    component: () => import('@/layouts/AuthLayout.vue'),
    children: [
      {
        path: '',
        name: 'reset-password',
        component: () => import('@/modules/auth/pages/ResetPasswordPage.vue'),
        meta: { title: 'Redefinir senha' },
      },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/components/NotFoundPage.vue'),
    meta: { title: 'Página não encontrada' },
  },
]
