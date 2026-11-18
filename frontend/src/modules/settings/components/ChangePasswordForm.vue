<script setup lang="ts">
import { reactive, ref } from 'vue'
import Button from 'primevue/button'
import Password from 'primevue/password'
import { useToast } from 'primevue/usetoast'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import {
  PASSWORD_MAX,
  validatePasswordConfirmation,
  validatePasswordPolicy,
  validateRequiredPassword,
} from '@/utils/validation'

const auth = useAuthStore()
const toast = useToast()

const form = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const { errors, clear, setLocal, setServer } = useFormErrors([
  'currentPassword',
  'newPassword',
  'confirmPassword',
] as const)
const loading = ref(false)

function reset(): void {
  form.currentPassword = ''
  form.newPassword = ''
  form.confirmPassword = ''
  clear()
}

async function onSubmit(): Promise<void> {
  const valid = setLocal({
    currentPassword: validateRequiredPassword(form.currentPassword),
    newPassword:
      validatePasswordPolicy(form.newPassword) ??
      (form.newPassword === form.currentPassword ? 'A nova senha deve ser diferente da atual.' : undefined),
    confirmPassword: validatePasswordConfirmation(form.newPassword, form.confirmPassword),
  })
  if (!valid) return

  loading.value = true
  try {
    await auth.changePassword({ currentPassword: form.currentPassword, newPassword: form.newPassword })
    reset()
    toast.add({
      severity: 'success',
      summary: 'Senha alterada',
      detail: 'As demais sessões foram encerradas. Esta sessão continua ativa.',
      life: 5000,
    })
  } catch (error) {
    const appError = toAppError(error)
    if (appError.code === 'invalid_password') {
      errors.currentPassword = appError.message
      return
    }
    if (!setServer(appError.fieldErrors)) {
      toast.add({
        severity: 'error',
        summary: 'Não foi possível alterar a senha',
        detail: appError.message,
        life: 5000,
      })
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <section class="surface-card" aria-labelledby="password-title">
    <h2 id="password-title" class="settings-section__title">Alterar senha</h2>
    <p class="settings-section__description">
      Ao alterar a senha, as sessões abertas em outros dispositivos serão encerradas.
    </p>

    <form class="form-stack" novalidate @submit.prevent="onSubmit">
      <FormField
        id="current-password"
        v-slot="{ id, describedBy, invalid }"
        label="Senha atual"
        :error="errors.currentPassword"
        required
      >
        <Password
          v-model="form.currentPassword"
          :input-id="id"
          name="currentPassword"
          :feedback="false"
          toggle-mask
          :invalid="invalid"
          :input-props="{ autocomplete: 'current-password', 'aria-describedby': describedBy, 'aria-invalid': invalid }"
          fluid
          @input="clear('currentPassword')"
        />
      </FormField>

      <FormField
        id="new-password"
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
        id="confirm-new-password"
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

      <div class="form-actions">
        <Button type="submit" label="Alterar senha" icon="pi pi-lock" :loading="loading" />
      </div>
    </form>
  </section>
</template>

<style src="../settings-section.css" scoped></style>
