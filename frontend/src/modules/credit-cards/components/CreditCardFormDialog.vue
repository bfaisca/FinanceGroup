<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useCreditCardsStore } from '@/stores/creditCards'
import type { CreditCardDto, CreditCardRequest } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { CREDIT_CARD_FORM_FIELDS } from '../validation'
import CreditCardForm from './CreditCardForm.vue'

const props = withDefaults(defineProps<{ card?: CreditCardDto | null }>(), { card: null })
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [card: CreditCardDto] }>()

const toast = useToast()
const cards = useCreditCardsStore()
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

async function onSubmit(request: CreditCardRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const editing = props.card !== null
    const saved = props.card ? await cards.update(props.card.id, request) : await cards.create(request)
    visible.value = false
    toast.add({ severity: 'success', summary: editing ? 'Cartão atualizado' : 'Cartão criado', life: 3500 })
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (CREDIT_CARD_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    // 409 (nome duplicado) e 422 aparecem no topo do formulário.
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
    :header="card ? 'Editar cartão' : 'Novo cartão'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(32rem, calc(100vw - 1.5rem))' }"
  >
    <CreditCardForm
      :key="formKey"
      :card="card"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
