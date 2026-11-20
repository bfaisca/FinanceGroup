<script setup lang="ts">
import type { TransactionDto } from '@/types/api'

/**
 * Selos de origem da movimentação (§10): cartão, parcela "3/12" e recorrência.
 * Com `linked`, parcela e recorrência levam ao plano/à regra; em cartões clicáveis (mobile)
 * ficam como texto, pois um link dentro de um botão não é válido.
 */
withDefaults(
  defineProps<{
    transaction: Pick<TransactionDto, 'creditCardName' | 'installment' | 'recurrenceRuleId'>
    linked?: boolean
  }>(),
  { linked: false },
)
</script>

<template>
  <span
    v-if="transaction.creditCardName || transaction.installment || transaction.recurrenceRuleId"
    class="origin-badges"
  >
    <span v-if="transaction.creditCardName" class="origin-badge" data-testid="badge-card">
      <i class="pi pi-credit-card" aria-hidden="true" />
      <span class="sr-only">Cartão: </span>{{ transaction.creditCardName }}
    </span>

    <template v-if="transaction.installment">
      <RouterLink
        v-if="linked"
        class="origin-badge origin-badge--link"
        data-testid="badge-installment"
        :to="{ name: 'installments', query: { open: transaction.installment.planId } }"
        :aria-label="`Parcela ${transaction.installment.number} de ${transaction.installment.count}. Abrir plano de parcelamento`"
      >
        <i class="pi pi-list-check" aria-hidden="true" />
        {{ transaction.installment.number }}/{{ transaction.installment.count }}
      </RouterLink>
      <span v-else class="origin-badge" data-testid="badge-installment">
        <i class="pi pi-list-check" aria-hidden="true" />
        <span class="sr-only">Parcela </span>{{ transaction.installment.number }}/{{ transaction.installment.count }}
      </span>
    </template>

    <template v-if="transaction.recurrenceRuleId">
      <RouterLink
        v-if="linked"
        class="origin-badge origin-badge--link"
        data-testid="badge-recurrence"
        :to="{ name: 'recurrences', query: { open: transaction.recurrenceRuleId } }"
        aria-label="Ocorrência de recorrência. Abrir regra"
      >
        <i class="pi pi-sync" aria-hidden="true" /> Recorrente
      </RouterLink>
      <span v-else class="origin-badge" data-testid="badge-recurrence">
        <i class="pi pi-sync" aria-hidden="true" /> Recorrente
      </span>
    </template>
  </span>
</template>

<style scoped>
.origin-badges {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.375rem;
  align-items: center;
}

.origin-badge {
  display: inline-flex;
  gap: 0.25rem;
  align-items: center;
  padding: 0 0.5rem;
  border-radius: 999px;
  border: 1px solid var(--gf-border);
  background: var(--p-content-hover-background);
  color: var(--gf-text);
  font-size: 0.75rem;
  font-weight: 600;
  line-height: 1.6;
  white-space: nowrap;
}

.origin-badge i {
  font-size: 0.6875rem;
}

.origin-badge--link {
  text-decoration: none;
}

.origin-badge--link:hover {
  text-decoration: underline;
}
</style>
