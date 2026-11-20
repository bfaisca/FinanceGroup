<script setup lang="ts">
import { computed, onMounted } from 'vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import FormField from '@/components/FormField.vue'
import { useAccountsStore } from '@/stores/accounts'
import { useCreditCardsStore } from '@/stores/creditCards'
import type { PaymentMethod } from '@/types/api'
import { enumOptions, PAYMENT_METHOD_LABELS } from '@/utils/labels'

export type PayWith = 'account' | 'card'

/**
 * Campos "Pago com": conta/dinheiro ou cartão de crédito (docs §10). Com cartão, conta e vencimento
 * não são enviados (o vencimento é calculado pela fatura no backend) e o método é fixo `CreditCard`.
 * Reutilizado por movimentações, parcelamentos e recorrências.
 */
const props = withDefaults(
  defineProps<{
    idPrefix: string
    /** Despesas podem usar cartão; receitas não. */
    allowCard?: boolean
    /** Escolha conta/cartão imutável (edição de movimentação). */
    locked?: boolean
    /** Conta/cartão atuais (podem estar inativos) continuam visíveis nos seletores. */
    currentAccountId?: string | null
    currentCardId?: string | null
    errors?: { creditCardId?: string; accountId?: string; paymentMethod?: string }
  }>(),
  { allowCard: true, locked: false, currentAccountId: null, currentCardId: null, errors: () => ({}) },
)

const payWith = defineModel<PayWith>('payWith', { required: true })
const creditCardId = defineModel<string | null>('creditCardId', { required: true })
const accountId = defineModel<string | null>('accountId', { required: true })
const paymentMethod = defineModel<PaymentMethod | null>('paymentMethod', { required: true })

const accounts = useAccountsStore()
const cards = useCreditCardsStore()

onMounted(() => {
  void accounts.load()
  if (props.allowCard) void cards.load()
})

const payWithOptions = [
  { value: 'account' as PayWith, label: 'Conta/dinheiro' },
  { value: 'card' as PayWith, label: 'Cartão de crédito' },
]
const methodOptions = enumOptions(PAYMENT_METHOD_LABELS)

const accountOptions = computed(() =>
  accounts.items
    .filter((a) => a.isActive || a.id === props.currentAccountId)
    .map((a) => ({ value: a.id, label: a.isActive ? a.name : `${a.name} (inativa)` })),
)

const cardOptions = computed(() =>
  cards.items
    .filter((c) => c.isActive || c.id === props.currentCardId)
    .map((c) => ({ value: c.id, label: c.isActive ? c.name : `${c.name} (inativo)` })),
)

const selectedCard = computed(() => (creditCardId.value ? cards.byId.get(creditCardId.value) : undefined))
const cardMode = computed(() => props.allowCard && payWith.value === 'card')
const noCards = computed(() => cards.loaded && cards.activeItems.length === 0)

function onPayWithChange(value: PayWith | null): void {
  if (!value || value === payWith.value) return
  payWith.value = value
  if (value === 'card') {
    // Compra no cartão: conta não é enviada e o método é fixo.
    accountId.value = null
    paymentMethod.value = 'CreditCard'
  } else {
    creditCardId.value = null
    if (paymentMethod.value === 'CreditCard') paymentMethod.value = null
  }
}
</script>

<template>
  <div class="payment-source">
    <fieldset v-if="allowCard" class="payment-source__choice">
      <legend>Pago com</legend>
      <SelectButton
        :model-value="payWith"
        :options="payWithOptions"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        :disabled="locked"
        aria-label="Pago com"
        :data-testid="`${idPrefix}-paywith`"
        @update:model-value="onPayWithChange"
      />
      <small v-if="locked" class="payment-source__hint">
        A forma de pagamento (conta ou cartão) não pode ser alterada depois que a movimentação é criada.
      </small>
    </fieldset>

    <template v-if="cardMode">
      <FormField
        :id="`${idPrefix}-card`"
        v-slot="{ id, describedBy, invalid }"
        label="Cartão de crédito"
        :error="errors.creditCardId"
        required
      >
        <Select
          v-model="creditCardId"
          :input-id="id"
          :options="cardOptions"
          option-label="label"
          option-value="value"
          placeholder="Escolha um cartão"
          :disabled="locked"
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
        />
      </FormField>
      <p v-if="noCards && !locked" class="payment-source__hint" data-testid="no-cards-hint">
        <i class="pi pi-info-circle" aria-hidden="true" />
        Você ainda não tem cartões ativos. <RouterLink :to="{ name: 'credit-cards' }">Cadastre um cartão</RouterLink>.
      </p>
      <p class="payment-source__hint" data-testid="invoice-due-hint">
        <i class="pi pi-info-circle" aria-hidden="true" />
        O vencimento é calculado pela fatura do cartão.
        <template v-if="selectedCard?.accountName"> Conta associada: {{ selectedCard.accountName }}.</template>
      </p>
    </template>

    <div v-else class="form-grid">
      <FormField
        :id="`${idPrefix}-account`"
        v-slot="{ id, describedBy, invalid }"
        label="Conta"
        :error="errors.accountId"
        hint="Opcional."
      >
        <Select
          v-model="accountId"
          :input-id="id"
          :options="accountOptions"
          option-label="label"
          option-value="value"
          placeholder="Sem conta"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
        />
      </FormField>
      <FormField
        :id="`${idPrefix}-method`"
        v-slot="{ id, describedBy, invalid }"
        label="Método de pagamento"
        :error="errors.paymentMethod"
        hint="Opcional."
      >
        <Select
          v-model="paymentMethod"
          :input-id="id"
          :options="methodOptions"
          option-label="label"
          option-value="value"
          placeholder="Não informado"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
        />
      </FormField>
    </div>

    <FormField v-if="cardMode" :id="`${idPrefix}-method-fixed`" v-slot="{ id }" label="Método de pagamento">
      <InputText :id="id" model-value="Cartão de crédito" readonly disabled fluid />
    </FormField>
  </div>
</template>

<style scoped>
.payment-source {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.payment-source__choice {
  margin: 0;
  padding: 0;
  border: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.payment-source__choice legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 500;
  font-size: 0.9375rem;
}

.payment-source__hint {
  margin: 0;
  color: var(--gf-text-muted);
  font-size: 0.8125rem;
}
</style>
