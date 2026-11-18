<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import Password from 'primevue/password'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import FormField from '@/components/FormField.vue'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import { validateRequiredPassword } from '@/utils/validation'

const auth = useAuthStore()
const router = useRouter()
const toast = useToast()

const dialogVisible = ref(false)
const password = ref('')
const passwordError = ref<string | undefined>()
const loading = ref(false)

function open(): void {
  password.value = ''
  passwordError.value = undefined
  dialogVisible.value = true
}

function onCancel(): void {
  password.value = ''
  passwordError.value = undefined
}

async function onConfirm(): Promise<void> {
  passwordError.value = validateRequiredPassword(password.value)
  if (passwordError.value) return

  loading.value = true
  try {
    await auth.deleteAccount(password.value)
    dialogVisible.value = false
    toast.add({ severity: 'info', summary: 'Conta excluída', detail: 'Sua conta foi excluída.', life: 5000 })
    await router.replace({ name: 'login' })
  } catch (error) {
    const appError = toAppError(error)
    passwordError.value =
      appError.code === 'invalid_password' ? appError.message : (appError.fieldErrors.password?.[0] ?? undefined)
    if (!passwordError.value) {
      toast.add({
        severity: 'error',
        summary: 'Não foi possível excluir a conta',
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
  <section class="surface-card settings-danger" aria-labelledby="delete-title">
    <h2 id="delete-title" class="settings-section__title">Excluir conta</h2>
    <p class="settings-section__description">
      Esta ação é permanente: sua conta será anonimizada e desativada, e todas as sessões serão encerradas. Não será
      possível acessá-la novamente.
    </p>
    <div class="form-actions">
      <Button label="Excluir minha conta" icon="pi pi-trash" severity="danger" outlined @click="open" />
    </div>

    <AppConfirmDialog
      v-model:visible="dialogVisible"
      header="Excluir conta"
      message="Tem certeza? Esta ação não pode ser desfeita. Digite sua senha para confirmar."
      confirm-label="Excluir definitivamente"
      severity="danger"
      :loading="loading"
      :confirm-disabled="password.length === 0"
      @confirm="onConfirm"
      @cancel="onCancel"
    >
      <FormField
        id="delete-password"
        v-slot="{ id, describedBy, invalid }"
        label="Senha"
        :error="passwordError"
        required
      >
        <Password
          v-model="password"
          :input-id="id"
          name="password"
          :feedback="false"
          toggle-mask
          :invalid="invalid"
          :input-props="{ autocomplete: 'current-password', 'aria-describedby': describedBy, 'aria-invalid': invalid }"
          fluid
          @input="passwordError = undefined"
        />
      </FormField>
    </AppConfirmDialog>
  </section>
</template>

<style src="../settings-section.css" scoped></style>
