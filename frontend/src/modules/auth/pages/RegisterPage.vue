<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import RegisterForm from '../components/RegisterForm.vue'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import { isInvitationRedirect, safeRedirect } from '@/utils/redirect'
import type { RegisterRequest } from '@/types/api'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const toast = useToast()

const fromInvitation = computed(() => isInvitationRedirect(route.query.redirect))

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})

async function onSubmit(request: RegisterRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const user = await auth.register(request)
    toast.add({ severity: 'success', summary: 'Conta criada', detail: `Bem-vindo(a), ${user.name}!`, life: 4000 })
    await router.replace(safeRedirect(route.query.redirect, '/'))
  } catch (error) {
    const appError = toAppError(error)
    if (appError.code === 'email_already_registered') {
      fieldErrors.value = { email: [appError.message] }
      return
    }
    fieldErrors.value = appError.fieldErrors
    errorMessage.value = Object.keys(appError.fieldErrors).length > 0 ? null : appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <header class="auth-page__header">
      <h1>Criar conta</h1>
      <p>Comece a organizar suas finanças pessoais.</p>
    </header>

    <Message v-if="fromInvitation" severity="info" class="auth-page__notice" data-testid="invitation-notice">
      Você recebeu um convite para um grupo. Crie sua conta com o e-mail do convite para aceitá-lo.
    </Message>

    <RegisterForm :loading="loading" :error-message="errorMessage" :field-errors="fieldErrors" @submit="onSubmit" />

    <p class="auth-page__alt">
      Já tem conta?
      <RouterLink :to="{ name: 'login', query: route.query.redirect ? { redirect: route.query.redirect } : {} }"
        >Entrar</RouterLink
      >
    </p>
  </div>
</template>

<style src="../auth-page.css" scoped></style>
