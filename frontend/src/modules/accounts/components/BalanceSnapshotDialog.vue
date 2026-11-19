<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useClockStore } from '@/stores/clock'
import type { BalanceSnapshotDto } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { accountService } from '../services/accountService'
import {
  buildSnapshotRequest,
  SNAPSHOT_FORM_FIELDS,
  validateSnapshotForm,
  type SnapshotFormValues,
} from '../validation'

const props = defineProps<{ accountId: string; accountName: string }>()
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [snapshot: BalanceSnapshotDto] }>()

const toast = useToast()
const clock = useClockStore()
useRestoreFocus(visible)

const { errors, clear, setLocal, setServer } = useFormErrors(SNAPSHOT_FORM_FIELDS)
const form = reactive<SnapshotFormValues>({ balance: '', referenceDate: clock.today })
const loading = ref(false)
const errorMessage = ref<string | null>(null)

watch(visible, (value) => {
  if (value) {
    form.balance = ''
    form.referenceDate = clock.today
    errorMessage.value = null
    setLocal({})
  }
})

async function onSubmit(): Promise<void> {
  if (!setLocal(validateSnapshotForm(form, { today: clock.today }))) return
  loading.value = true
  errorMessage.value = null
  try {
    const snapshot = await accountService.createSnapshot(props.accountId, buildSnapshotRequest(form))
    visible.value = false
    toast.add({
      severity: 'success',
      summary: 'Saldo informado',
      detail: 'O saldo projetado foi recalculado.',
      life: 4000,
    })
    emit('saved', snapshot)
  } catch (error) {
    const appError = toAppError(error)
    const mapped = setServer(appError.fieldErrors)
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
    header="Informar novo saldo"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(30rem, calc(100vw - 1.5rem))' }"
  >
    <form class="form-stack" novalidate aria-label="Informar novo saldo" @submit.prevent="onSubmit">
      <p class="snapshot-intro">
        Informe o saldo real de <strong>{{ accountName }}</strong> em uma data. Ele passa a ser a referência para o
        saldo projetado; os saldos anteriores continuam no histórico.
      </p>
      <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

      <FormField
        id="snap-balance"
        v-slot="{ id, describedBy, invalid }"
        label="Saldo"
        :error="errors.balance"
        hint="Pode ser negativo."
        required
      >
        <MoneyInput
          :id="id"
          v-model="form.balance"
          allow-negative
          autofocus
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('balance')"
        />
      </FormField>
      <FormField
        id="snap-date"
        v-slot="{ id, describedBy, invalid }"
        label="Data de referência"
        :error="errors.referenceDate"
        hint="Hoje ou uma data passada."
        required
      >
        <DateInput
          :id="id"
          v-model="form.referenceDate"
          :max="clock.today"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('referenceDate')"
        />
      </FormField>

      <div class="form-actions">
        <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="visible = false" />
        <Button type="submit" label="Salvar saldo" icon="pi pi-check" :loading="loading" />
      </div>
    </form>
  </Dialog>
</template>

<style scoped>
.snapshot-intro {
  margin: 0;
}
</style>
