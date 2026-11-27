<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Message from 'primevue/message'
import RadioButton from 'primevue/radiobutton'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import LoadingState from '@/components/LoadingState.vue'
import { useClockStore } from '@/stores/clock'
import type { CardInvoiceDto, CreditCardDto, DateOnlyString } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { creditCardService } from '../services/creditCardService'

/**
 * Pagar fatura (D-057): escolhe a fatura e dá baixa, de uma só vez, no restante de todas as compras dela.
 * Não cria despesa nova e não permite pagamento parcial da fatura.
 */
const props = defineProps<{ card: CreditCardDto | null }>()
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ paid: [] }>()

const toast = useToast()
const clock = useClockStore()

const invoices = ref<CardInvoiceDto[]>([])
const selected = ref<DateOnlyString | null>(null)
const paymentDate = ref<DateOnlyString | null>(null)
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)

const current = computed(() => invoices.value.find((i) => i.dueDate === selected.value) ?? null)
const dateError = computed(() => {
  if (!paymentDate.value) return 'Informe a data do pagamento.'
  if (paymentDate.value > clock.today) return 'A data do pagamento não pode ser futura.'
  return null
})

async function load(): Promise<void> {
  if (!props.card) return
  loading.value = true
  error.value = null
  invoices.value = []
  selected.value = null
  paymentDate.value = clock.today
  try {
    invoices.value = await creditCardService.listInvoices(props.card.id)
    // Pré-seleciona a fatura aberta (a que recebe compras de hoje) quando tem valor; senão, a mais antiga.
    const open = invoices.value.find((i) => i.dueDate === props.card?.openInvoice.dueDate)
    selected.value = (open ?? invoices.value[0])?.dueDate ?? null
  } catch (e) {
    error.value = toAppError(e).message
  } finally {
    loading.value = false
  }
}

watch(visible, (value) => {
  if (value) void load()
})

async function confirm(): Promise<void> {
  if (!props.card || !current.value || !paymentDate.value || dateError.value) return
  saving.value = true
  try {
    const result = await creditCardService.payInvoice(props.card.id, current.value.dueDate, paymentDate.value)
    toast.add({
      severity: 'success',
      summary: 'Fatura paga',
      detail: `${formatCurrency(result.paidAmount)} em ${result.purchasesPaid} ${result.purchasesPaid === 1 ? 'compra' : 'compras'}.`,
      life: 4500,
    })
    visible.value = false
    emit('paid')
  } catch (e) {
    error.value = toAppError(e).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <AppConfirmDialog
    v-model:visible="visible"
    :header="`Pagar fatura${card ? ` — ${card.name}` : ''}`"
    confirm-label="Pagar fatura"
    :loading="saving"
    :confirm-disabled="loading || !current || dateError !== null"
    @confirm="confirm"
  >
    <LoadingState v-if="loading" label="Carregando faturas…" />

    <Message v-else-if="invoices.length === 0 && !error" severity="info" :closable="false" data-testid="no-invoices">
      Não há faturas em aberto neste cartão.
    </Message>

    <template v-else-if="invoices.length > 0">
      <fieldset class="pay-invoice__list">
        <legend>Fatura a pagar</legend>
        <label v-for="invoice in invoices" :key="invoice.dueDate" class="pay-invoice__option">
          <RadioButton v-model="selected" name="invoice" :value="invoice.dueDate" />
          <span>
            <strong>Vence em {{ formatDate(invoice.dueDate) }}</strong>
            <span v-if="invoice.isOverdue" class="pay-invoice__overdue"> · atrasada</span>
            <br />
            <span class="pay-invoice__muted">
              {{ invoice.purchaseCount }} {{ invoice.purchaseCount === 1 ? 'compra' : 'compras' }} ·
              <span class="money">{{ formatCurrency(invoice.remainingAmount) }}</span>
            </span>
          </span>
        </label>
      </fieldset>

      <FormField id="pay-invoice-date" label="Data do pagamento" required :error="dateError ?? undefined">
        <DateInput id="pay-invoice-date" v-model="paymentDate" :max="clock.today" :invalid="dateError !== null" />
      </FormField>

      <p class="pay-invoice__muted" data-testid="pay-invoice-summary">
        Dá baixa no restante de todas as compras desta fatura
        <template v-if="current">({{ formatCurrency(current.remainingAmount) }})</template>. Nenhuma despesa nova é
        criada.
      </p>
    </template>

    <Message v-if="error" severity="error" role="alert">{{ error }}</Message>
  </AppConfirmDialog>
</template>

<style scoped>
.pay-invoice__list {
  display: grid;
  gap: 0.5rem;
  margin: 0 0 1rem;
  padding: 0;
  border: 0;
}

.pay-invoice__list legend {
  margin-bottom: 0.5rem;
  font-weight: 600;
}

.pay-invoice__option {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
  padding: 0.625rem 0.75rem;
  border: 1px solid var(--gf-border);
  border-radius: 8px;
  cursor: pointer;
}

.pay-invoice__muted {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.pay-invoice__overdue {
  color: var(--p-red-600, #b91c1c);
  font-weight: 600;
}
</style>
