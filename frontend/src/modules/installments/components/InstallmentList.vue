<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import { BREAKPOINTS, useMediaQuery } from '@/composables/useMediaQuery'
import TransactionStatusChip from '@/modules/transactions/components/TransactionStatusChip.vue'
import type { TransactionDto } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'

/**
 * Parcelas de um plano, em ordem de número, exatamente como a API devolve (sem cálculo local).
 * Tablet+ = tabela; mobile = cartões. O botão "Abrir" tem handler próprio.
 */
const props = withDefaults(defineProps<{ installments: TransactionDto[]; caption?: string; showActions?: boolean }>(), {
  caption: undefined,
  showActions: true,
})
const emit = defineEmits<{ open: [transaction: TransactionDto] }>()

const isTabletUp = useMediaQuery(BREAKPOINTS.tablet)
const actions = computed(() => props.showActions)
</script>

<template>
  <div>
    <div v-if="isTabletUp" class="table-scroll">
      <table class="simple-table" data-testid="installments-table">
        <caption class="sr-only">
          {{
            caption ?? 'Parcelas'
          }}
        </caption>
        <thead>
          <tr>
            <th scope="col">Nº</th>
            <th scope="col">Data</th>
            <th scope="col">Vencimento</th>
            <th scope="col" class="num">Valor</th>
            <th scope="col">Situação</th>
            <th scope="col" class="num">Pago</th>
            <th scope="col" class="num">Restante</th>
            <th v-if="actions" scope="col"><span class="sr-only">Ações</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="tx in installments" :key="tx.id">
            <th scope="row">{{ tx.installment ? `${tx.installment.number}/${tx.installment.count}` : '—' }}</th>
            <td>{{ formatDate(tx.transactionDate) }}</td>
            <td>{{ formatDate(tx.dueDate) }}</td>
            <td class="num money">{{ formatCurrency(tx.amount) }}</td>
            <td><TransactionStatusChip :status="tx.status" /></td>
            <td class="num money">{{ formatCurrency(tx.paidAmount) }}</td>
            <td class="num money">{{ formatCurrency(tx.remainingAmount) }}</td>
            <td v-if="actions">
              <Button
                type="button"
                label="Abrir"
                icon="pi pi-external-link"
                size="small"
                severity="secondary"
                text
                :aria-label="`Abrir parcela ${tx.installment?.number ?? ''} de ${tx.installment?.count ?? ''}`"
                @click="emit('open', tx)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <ul v-else class="installment-cards" :aria-label="caption ?? 'Parcelas'">
      <li v-for="tx in installments" :key="tx.id" class="installment-card">
        <div class="installment-card__row">
          <strong>Parcela {{ tx.installment ? `${tx.installment.number}/${tx.installment.count}` : '' }}</strong>
          <TransactionStatusChip :status="tx.status" />
        </div>
        <dl class="installment-card__facts">
          <div>
            <dt>Data</dt>
            <dd>{{ formatDate(tx.transactionDate) }}</dd>
          </div>
          <div>
            <dt>Vencimento</dt>
            <dd>{{ formatDate(tx.dueDate) }}</dd>
          </div>
          <div>
            <dt>Valor</dt>
            <dd class="money">{{ formatCurrency(tx.amount) }}</dd>
          </div>
          <div>
            <dt>Restante</dt>
            <dd class="money">{{ formatCurrency(tx.remainingAmount) }}</dd>
          </div>
        </dl>
        <Button
          v-if="actions"
          type="button"
          label="Abrir"
          icon="pi pi-external-link"
          size="small"
          severity="secondary"
          outlined
          :aria-label="`Abrir parcela ${tx.installment?.number ?? ''} de ${tx.installment?.count ?? ''}`"
          @click="emit('open', tx)"
        />
      </li>
    </ul>
  </div>
</template>

<style scoped>
.installment-cards {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.installment-card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
}

.installment-card__row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}

.installment-card__facts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.375rem 1rem;
  margin: 0;
  font-size: 0.875rem;
}

.installment-card__facts dt {
  color: var(--gf-text-muted);
  font-size: 0.75rem;
}

.installment-card__facts dd {
  margin: 0;
}
</style>
