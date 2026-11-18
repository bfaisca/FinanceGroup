<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Password from 'primevue/password'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { authService } from '../services/authService'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import { PASSWORD_MAX, validatePasswordConfirmation, validatePasswordPolicy } from '@/utils/validation'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

const token = computed(() => {
  const value = route.query.token
  const raw = Array.isArray(value) ? value[0] : value
  return typeof raw === 'string' && raw.trim().length > 0 ? raw : null
})

const form = reactive({ newPassword: '', confirmPassword: '' })
const { errors, clear, setLocal, setServer } = useFormErrors(['newPassword', 'confirmPassword'] as const)
const loading = ref(false)
const tokenInvalid = ref(false)
const errorMessage = ref<string | null>(null)

async function onSubmit(): Promise<void> {
  if (!token.value) return
  const valid = setLocal({
    newPassword: validatePasswordPolicy(form.newPassword),
    confirmPassword: validatePasswordConfirmation(form.newPassword, form.confirmPassword),
  })
  if (!valid) return

  loading.value = true
  errorMessage.value = null
  try {
    await authService.resetPassword({ token: token.value, newPassword: form.newPassword })
    // Todos os refresh tokens foram revogados no servidor: encerra qualquer sessão local.
    auth.clearSession()
    toast.add({
      severity: 'success',
      summary: 'Senha redefinida',
      detail: 'Entre com sua nova senha.',
      life: 5000,
    })
    await router.replace({ name: 'login' })
  } catch (error) {
    const appError = toAppError(error)
    if (appError.code === 'invalid_token') {
      tokenInvalid.value = true
      return
    }
    if (!setServer(appError.fieldErrors)) errorMessage.value = appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <header class="auth-page__header">
      <h1>Redefinir senha</h1>
      <p v-if="token && !tokenInvalid">Escolha uma nova senha para sua conta.</p>
    </header>

    <template v-if="!token || tokenInvalid">
      <Message severity="error" role="alert">
        Este link de redefinição é inválido ou expirou. Solicite um novo link para redefinir sua senha.
      </Message>
      <RouterLink v-slot="{ navigate, href }" :to="{ name: 'forgot-password' }" custom>
        <Button as="a" :href="href" label="Solicitar novo link" icon="pi pi-envelope" fluid @click="navigate" />
      </RouterLink>
    </template>

    <form v-else class="form-stack" novalidate aria-label="Redefinir senha" @submit.prevent="onSubmit">
      <Message v-if="errorMessage" severity="error" role="alert">{{ errorMessage }}</Message>

      <FormField
        id="reset-new-password"
        v-slot="{ id, describedBy, invalid }"
        label="Nova senha"
        hint="De 8 a 128 caracteres, com ao menos uma letra e um número."
        :error="errors.newPassword"
        required
      >
        <Password
          v-model="form.newPassword"
          :input-id="id"
          name="newPassword"
          :feedback="false"
          toggle-mask
          :invalid="invalid"
          :input-props="{
            autocomplete: 'new-password',
            maxlength: PASSWORD_MAX,
            'aria-describedby': describedBy,
            'aria-invalid': invalid,
          }"
          fluid
          @input="clear('newPassword')"
        />
      </FormField>

      <FormField
        id="reset-confirm-password"
        v-slot="{ id, describedBy, invalid }"
        label="Confirmar nova senha"
        :error="errors.confirmPassword"
        required
      >
        <Password
          v-model="form.confirmPassword"
          :input-id="id"
          name="confirmPassword"
          :feedback="false"
          toggle-mask
          :invalid="invalid"
          :input-props="{ autocomplete: 'new-password', 'aria-describedby': describedBy, 'aria-invalid': invalid }"
          fluid
          @input="clear('confirmPassword')"
        />
      </FormField>

      <Button type="submit" label="Redefinir senha" icon="pi pi-check" :loading="loading" fluid />
    </form>

    <p class="auth-page__alt"><RouterLink :to="{ name: 'login' }">Voltar para o login</RouterLink></p>
  </div>
</template>

<style src="../auth-page.css" scoped></style>
