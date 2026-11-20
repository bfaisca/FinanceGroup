<script setup lang="ts">
import Button from 'primevue/button'
import ToneChip from '@/components/ToneChip.vue'
import type { CreditCardDto } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'
import LimitBar from './LimitBar.vue'

defineProps<{ card: CreditCardDto; busy?: boolean }>()
const emit = defineEmits<{
  edit: [card: CreditCardDto]
  toggle: [card: CreditCardDto]
  purchases: [card: CreditCardDto]
}>()
</script>

<template>
  <article class="card-item" :class="{ 'card-item--inactive': !card.isActive }" :aria-labelledby="`card-${card.id}`">
    <header class="card-item__header">
      <div>
        <h2 :id="`card-${card.id}`" class="card-item__name">{{ card.name }}</h2>
        <p v-if="card.institution" class="card-item__muted">{{ card.institution }}</p>
      </div>
      <ToneChip v-if="!card.isActive" label="Inativo" icon="pi pi-ban" />
    </header>

    <LimitBar
      :used-limit="card.usedLimit"
      :credit-limit="card.creditLimit"
      :available-limit="card.availableLimit"
      :labelled-by="`card-${card.id}`"
    />

    <p class="card-item__cycle" data-testid="card-cycle">
      <i class="pi pi-calendar" aria-hidden="true" />
      Fecha dia {{ card.closingDay }} · vence dia {{ card.dueDay }}
    </p>

    <section class="card-item__invoice" :aria-labelledby="`invoice-${card.id}`">
      <h3 :id="`invoice-${card.id}`" class="card-item__invoice-title">Fatura aberta (resumo)</h3>
      <p class="card-item__invoice-value money" data-testid="open-invoice-amount">
        {{ formatCurrency(card.openInvoice.amount) }}
      </p>
      <p class="card-item__muted">
        Fecha em {{ formatDate(card.openInvoice.closingDate) }} · vence em {{ formatDate(card.openInvoice.dueDate) }}
      </p>
      <p class="card-item__note">
        Resumo simplificado das compras que entram na próxima fatura. O sistema não faz a gestão completa de faturas.
      </p>
    </section>

    <p v-if="card.accountName" class="card-item__muted" data-testid="card-account">
      <i class="pi pi-building-columns" aria-hidden="true" /> Conta associada: {{ card.accountName }}
    </p>

    <footer class="card-item__actions">
      <Button
        type="button"
        label="Ver compras"
        icon="pi pi-list"
        severity="secondary"
        text
        size="small"
        :aria-label="`Ver compras do cartão ${card.name}`"
        @click="emit('purchases', card)"
      />
      <Button
        type="button"
        label="Editar"
        icon="pi pi-pencil"
        severity="secondary"
        text
        size="small"
        :aria-label="`Editar o cartão ${card.name}`"
        :disabled="busy"
        @click="emit('edit', card)"
      />
      <Button
        type="button"
        :label="card.isActive ? 'Desativar' : 'Reativar'"
        :icon="card.isActive ? 'pi pi-ban' : 'pi pi-replay'"
        :severity="card.isActive ? 'danger' : 'secondary'"
        text
        size="small"
        :aria-label="`${card.isActive ? 'Desativar' : 'Reativar'} o cartão ${card.name}`"
        :disabled="busy"
        @click="emit('toggle', card)"
      />
    </footer>
  </article>
</template>

<style scoped>
.card-item {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.25rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
  height: 100%;
}

.card-item--inactive {
  border-style: dashed;
  opacity: 0.9;
}

.card-item__header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
}

.card-item__name {
  font-size: 1.0625rem;
  overflow-wrap: anywhere;
}

.card-item__muted,
.card-item__note {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.card-item__cycle {
  margin: 0;
  display: flex;
  gap: 0.5rem;
  align-items: center;
  font-weight: 500;
}

.card-item__invoice {
  padding: 0.75rem;
  border-radius: 8px;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
}

.card-item__invoice .card-item__muted,
.card-item__invoice .card-item__note {
  color: inherit;
  opacity: 0.9;
}

.card-item__invoice-title {
  font-size: 0.875rem;
  font-weight: 600;
}

.card-item__invoice-value {
  margin: 0.125rem 0 0.25rem;
  font-size: 1.25rem;
  font-weight: 700;
}

.card-item__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin-top: auto;
  padding-top: 0.5rem;
  border-top: 1px solid var(--gf-border);
}
</style>
