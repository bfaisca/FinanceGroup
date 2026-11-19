<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useAccountsStore } from '@/stores/accounts'
import { useClockStore } from '@/stores/clock'
import type { AccountDto, CreateAccountRequest, UpdateAccountRequest } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { ACCOUNT_FORM_FIELDS } from '../validation'
import AccountForm from './AccountForm.vue'

const props = withDefaults(defineProps<{ account?: AccountDto | null }>(), { account: null })
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [account: AccountDto] }>()

const toast = useToast()
const accounts = useAccountsStore()
const clock = useClockStore()
useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
const formKey = ref(0)

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
  }
})

async function onSubmit(request: CreateAccountRequest | UpdateAccountRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const editing = props.account !== null
    const saved = props.account
      ? await accounts.update(props.account.id, request as UpdateAccountRequest)
      : await accounts.create(request as CreateAccountRequest)
    visible.value = false
    toast.add({ severity: 'success', summary: editing ? 'Conta atualizada' : 'Conta criada', life: 3500 })
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (ACCOUNT_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    // 409 `conflict` (nome duplicado) e 422 mostram a mensagem no topo do formulário.
    errorMessage.value = mapped ? null : appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="account ? 'Editar conta' : 'Nova conta'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(32rem, calc(100vw - 1.5rem))' }"
  >
    <AccountForm
      :key="formKey"
      :account="account"
      :today="clock.today"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
