<script setup lang="ts">
import { reactive, ref } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { authService } from '../services/authService'
import { toAppError } from '@/utils/problem'
import { normalizeEmail, validateEmail } from '@/utils/validation'

const form = reactive({ email: '' })
const { errors, clear, setLocal, setServer } = useFormErrors(['email'] as const)
const loading = ref(false)
const sent = ref(false)
const errorMessage = ref<string | null>(null)

async function onSubmit(): Promise<void> {
  if (!setLocal({ email: validateEmail(form.email) })) return
  loading.value = true
  errorMessage.value = null
  try {
    await authService.forgotPassword({ email: normalizeEmail(form.email) })
    sent.value = true
  } catch (error) {
    const appError = toAppError(error)
    if (!setServer(appError.fieldErrors)) errorMessage.value = appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <header class="auth-page__header">
      <h1>Esqueci a senha</h1>
      <p>Informe seu e-mail para receber um link de redefinição.</p>
    </header>

    <template v-if="sent">
      <Message severity="success" role="status">
        Se o e-mail estiver cadastrado, você receberá em instantes um link para redefinir sua senha. O link é válido por
        1 hora.
      </Message>
      <Button label="Enviar para outro e-mail" severity="secondary" outlined fluid @click="sent = false" />
    </template>

    <form v-else class="form-stack" novalidate aria-label="Solicitar redefinição de senha" @submit.prevent="onSubmit">
      <Message v-if="errorMessage" severity="error" role="alert">{{ errorMessage }}</Message>
      <FormField id="forgot-email" v-slot="{ id, describedBy, invalid }" label="E-mail" :error="errors.email" required>
        <InputText
          :id="id"
          v-model="form.email"
          type="email"
          name="email"
          autocomplete="email"
          inputmode="email"
          :aria-describedby="describedBy"
          :aria-invalid="invalid"
          :invalid="invalid"
          fluid
          @input="clear('email')"
        />
      </FormField>
      <Button type="submit" label="Enviar link" icon="pi pi-envelope" :loading="loading" fluid />
    </form>

    <p class="auth-page__alt"><RouterLink :to="{ name: 'login' }">Voltar para o login</RouterLink></p>
  </div>
</template>

<style src="../auth-page.css" scoped></style>
