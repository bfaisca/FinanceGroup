<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useClockStore } from '@/stores/clock'
import type { CreateRecurrenceRequest, RecurrenceDto, UpdateRecurrenceRequest } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { recurrenceService } from '../services/recurrenceService'
import { RECURRENCE_FORM_FIELDS } from '../validation'
import RecurrenceForm from './RecurrenceForm.vue'

const props = withDefaults(defineProps<{ recurrence?: RecurrenceDto | null }>(), { recurrence: null })
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [recurrence: RecurrenceDto] }>()

const toast = useToast()
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

async function onSubmit(request: CreateRecurrenceRequest | UpdateRecurrenceRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const editing = props.recurrence !== null
    const saved = props.recurrence
      ? await recurrenceService.update(props.recurrence.id, request as UpdateRecurrenceRequest)
      : await recurrenceService.create(request as CreateRecurrenceRequest)
    visible.value = false
    toast.add({ severity: 'success', summary: editing ? 'Recorrência atualizada' : 'Recorrência criada', life: 3500 })
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (RECURRENCE_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
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
    :header="recurrence ? 'Editar recorrência' : 'Nova recorrência'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(40rem, calc(100vw - 1.5rem))' }"
  >
    <RecurrenceForm
      :key="formKey"
      :recurrence="recurrence"
      :today="clock.today"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
