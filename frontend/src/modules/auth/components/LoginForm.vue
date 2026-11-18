<script setup lang="ts">
import { reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Message from 'primevue/message'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { normalizeEmail, validateEmail, validateRequiredPassword } from '@/utils/validation'
import type { LoginRequest } from '@/types/api'

const props = withDefaults(
  defineProps<{
    loading?: boolean
    /** Mensagem geral (ex.: `invalid_credentials`). */
    errorMessage?: string | null
    /** `errors` do ProblemDetails (validation_error). */
    fieldErrors?: Record<string, string[]>
  }>(),
  { loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: LoginRequest] }>()

const form = reactive({ email: '', password: '' })
const { errors, clear, setLocal, setServer } = useFormErrors(['email', 'password'] as const)

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

function onSubmit(): void {
  const valid = setLocal({
    email: validateEmail(form.email),
    password: validateRequiredPassword(form.password),
  })
  if (!valid) return
  emit('submit', { email: normalizeEmail(form.email), password: form.password })
}
</script>

<template>
  <form class="form-stack" novalidate aria-label="Entrar" @submit.prevent="onSubmit">
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField id="login-email" v-slot="{ id, describedBy, invalid }" label="E-mail" :error="errors.email" required>
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

    <FormField
      id="login-password"
      v-slot="{ id, describedBy, invalid }"
      label="Senha"
      :error="errors.password"
      required
    >
      <Password
        v-model="form.password"
        :input-id="id"
        name="password"
        :feedback="false"
        toggle-mask
        :invalid="invalid"
        :input-props="{ autocomplete: 'current-password', 'aria-describedby': describedBy, 'aria-invalid': invalid }"
        fluid
        @input="clear('password')"
      />
    </FormField>

    <div class="login-form__links">
      <RouterLink :to="{ name: 'forgot-password' }">Esqueci minha senha</RouterLink>
    </div>

    <Button type="submit" label="Entrar" icon="pi pi-sign-in" :loading="loading" fluid />
  </form>
</template>

<style scoped>
.login-form__links {
  display: flex;
  justify-content: flex-end;
  font-size: 0.9375rem;
}
</style>
