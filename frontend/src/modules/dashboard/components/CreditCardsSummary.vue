<script setup lang="ts">
import Button from 'primevue/button'
import { useRouter } from 'vue-router'
import LimitBar from '@/modules/credit-cards/components/LimitBar.vue'
import type { DashboardCreditCard } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'

/** Resumo dos cartões ativos (§15): limite, utilizado, disponível e fatura aberta com vencimento. */
defineProps<{ cards: DashboardCreditCard[] }>()

const router = useRouter()
</script>

<template>
  <div>
    <p v-if="cards.length === 0" class="cards-summary__hint" data-testid="no-cards-hint">
      <i class="pi pi-info-circle" aria-hidden="true" />
      Você ainda não tem cartões de crédito ativos.
      <RouterLink :to="{ name: 'credit-cards' }">Cadastre um cartão</RouterLink>
      para acompanhar limite e fatura aqui.
    </p>

    <ul v-else class="cards-summary__list" aria-label="Cartões de crédito">
      <li v-for="card in cards" :key="card.id" class="cards-summary__item" data-testid="dashboard-card">
        <h3 :id="`dash-card-${card.id}`" class="cards-summary__name">{{ card.name }}</h3>
        <LimitBar
          :used-limit="card.usedLimit"
          :credit-limit="card.creditLimit"
          :available-limit="card.availableLimit"
          :labelled-by="`dash-card-${card.id}`"
        />
        <p class="cards-summary__invoice">
          Fatura aberta: <strong class="money">{{ formatCurrency(card.openInvoice.amount) }}</strong>
          <span class="cards-summary__muted"> · vence em {{ formatDate(card.openInvoice.dueDate) }}</span>
        </p>
      </li>
    </ul>

    <div v-if="cards.length > 0" class="cards-summary__footer">
      <Button
        type="button"
        label="Ver cartões"
        icon="pi pi-credit-card"
        severity="secondary"
        text
        size="small"
        @click="router.push({ name: 'credit-cards' })"
      />
    </div>
  </div>
</template>

<style scoped>
.cards-summary__hint {
  margin: 0;
  color: var(--gf-text-muted);
}

.cards-summary__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 18rem), 1fr));
  gap: 1rem;
}

.cards-summary__item {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  padding: 1rem;
  border: 1px solid var(--gf-border);
  border-radius: 10px;
}

.cards-summary__name {
  font-size: 1rem;
  font-weight: 600;
  overflow-wrap: anywhere;
}

.cards-summary__invoice {
  margin: 0;
  font-size: 0.875rem;
}

.cards-summary__muted {
  color: var(--gf-text-muted);
}

.cards-summary__footer {
  margin-top: 0.5rem;
}
</style>
