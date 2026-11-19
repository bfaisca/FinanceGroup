<script setup lang="ts">
import Button from 'primevue/button'
import type { AccountDto } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'
import { ACCOUNT_TYPE_LABELS } from '@/utils/labels'

defineProps<{ account: AccountDto; busy?: boolean }>()
const emit = defineEmits<{ edit: [account: AccountDto]; toggle: [account: AccountDto] }>()
</script>

<template>
  <article
    class="account-card"
    :class="{ 'account-card--inactive': !account.isActive }"
    :aria-labelledby="`account-${account.id}`"
  >
    <header class="account-card__header">
      <div class="account-card__title">
        <h2 :id="`account-${account.id}`">{{ account.name }}</h2>
        <p class="account-card__subtitle">
          {{ ACCOUNT_TYPE_LABELS[account.type]
          }}<template v-if="account.institution"> · {{ account.institution }}</template>
        </p>
      </div>
      <span v-if="!account.isActive" class="account-card__badge account-card__badge--inactive">
        <i class="pi pi-ban" aria-hidden="true" /> Inativa
      </span>
    </header>

    <div v-if="account.latestSnapshot" class="account-card__balances">
      <div>
        <p class="account-card__label">Saldo informado</p>
        <p class="account-card__value money">{{ formatCurrency(account.latestSnapshot.balance) }}</p>
        <p class="account-card__caption">em {{ formatDate(account.latestSnapshot.referenceDate) }}</p>
      </div>
      <div>
        <p class="account-card__label">
          Saldo projetado
          <span class="account-card__badge">Projetado</span>
        </p>
        <p class="account-card__value money" data-testid="projected-balance">
          {{ formatCurrency(account.projectedBalance) }}
        </p>
        <p class="account-card__caption">em {{ formatDate(account.projectedAsOf) }}</p>
      </div>
      <p class="account-card__note">
        Estimativa com base no último saldo informado e nas movimentações até {{ formatDate(account.projectedAsOf) }}.
        Não é o saldo bancário.
      </p>
    </div>
    <p v-else class="account-card__empty" data-testid="no-snapshot">
      <i class="pi pi-info-circle" aria-hidden="true" /> Informe um saldo para ver a projeção.
    </p>

    <footer class="account-card__actions">
      <RouterLink :to="{ name: 'account-detail', params: { id: account.id } }" class="account-card__link">
        <i class="pi pi-eye" aria-hidden="true" /> Detalhes
      </RouterLink>
      <Button
        type="button"
        label="Editar"
        icon="pi pi-pencil"
        severity="secondary"
        text
        size="small"
        :disabled="busy"
        @click="emit('edit', account)"
      />
      <Button
        type="button"
        :label="account.isActive ? 'Desativar' : 'Reativar'"
        :icon="account.isActive ? 'pi pi-ban' : 'pi pi-replay'"
        :severity="account.isActive ? 'danger' : 'secondary'"
        text
        size="small"
        :disabled="busy"
        @click="emit('toggle', account)"
      />
    </footer>
  </article>
</template>

<style scoped>
.account-card {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.25rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.account-card--inactive {
  opacity: 0.85;
  border-style: dashed;
}

.account-card__header {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
}

.account-card__title h2 {
  font-size: 1.0625rem;
  overflow-wrap: anywhere;
}

.account-card__subtitle,
.account-card__caption,
.account-card__note {
  margin: 0;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.account-card__balances {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
  gap: 1rem;
}

.account-card__label {
  margin: 0;
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
  align-items: center;
  font-size: 0.875rem;
  color: var(--gf-text-muted);
}

.account-card__value {
  margin: 0.125rem 0 0;
  font-size: 1.25rem;
  font-weight: 700;
}

.account-card__note {
  grid-column: 1 / -1;
}

.account-card__badge {
  display: inline-flex;
  gap: 0.25rem;
  align-items: center;
  padding: 0.0625rem 0.5rem;
  border-radius: 999px;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-size: 0.75rem;
  font-weight: 600;
  height: fit-content;
}

.account-card__badge--inactive {
  background: color-mix(in srgb, var(--gf-neutral) 14%, transparent);
  color: var(--gf-neutral);
}

.account-card__empty {
  margin: 0;
  padding: 0.75rem;
  border-radius: 8px;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
}

.account-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  align-items: center;
  margin-top: auto;
  padding-top: 0.5rem;
  border-top: 1px solid var(--gf-border);
}

.account-card__link {
  display: inline-flex;
  gap: 0.375rem;
  align-items: center;
  min-height: 2.25rem;
  padding: 0 0.75rem;
  font-weight: 500;
  font-size: 0.875rem;
}
</style>
