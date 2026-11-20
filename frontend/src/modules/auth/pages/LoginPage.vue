<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Message from 'primevue/message'
import LoginForm from '../components/LoginForm.vue'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import { isInvitationRedirect, safeRedirect } from '@/utils/redirect'
import type { LoginRequest } from '@/types/api'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})

const sessionExpired = computed(() => route.query.reason === 'expired')
const fromInvitation = computed(() => isInvitationRedirect(route.query.redirect))

async function onSubmit(request: LoginRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    await auth.login(request)
    await router.replace(safeRedirect(route.query.redirect, '/'))
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    errorMessage.value =
      appError.code === 'validation_error' && Object.keys(appError.fieldErrors).length > 0 ? null : appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <header class="auth-page__header">
      <h1>Entrar</h1>
      <p>Acesse sua conta para acompanhar suas finanças.</p>
    </header>

    <Message v-if="sessionExpired && !errorMessage" severity="warn" class="auth-page__notice">
      Sua sessão expirou. Entre novamente para continuar.
    </Message>

    <Message v-if="fromInvitation" severity="info" class="auth-page__notice" data-testid="invitation-notice">
      Você recebeu um convite para um grupo. Entre ou crie uma conta com o e-mail do convite para aceitá-lo.
    </Message>

    <LoginForm :loading="loading" :error-message="errorMessage" :field-errors="fieldErrors" @submit="onSubmit" />

    <p class="auth-page__alt">
      Ainda não tem conta?
      <RouterLink :to="{ name: 'register', query: route.query.redirect ? { redirect: route.query.redirect } : {} }"
        >Criar conta</RouterLink
      >
    </p>
  </div>
</template>

<style src="../auth-page.css" scoped></style>
