<script setup lang="ts">
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import ToneChip from '@/components/ToneChip.vue'
import { BREAKPOINTS, useMediaQuery } from '@/composables/useMediaQuery'
import TransactionAmount from '@/modules/transactions/components/TransactionAmount.vue'
import type { RecurrenceDto } from '@/types/api'
import { formatDate } from '@/utils/format'
import { frequencyLabel } from '@/utils/recurrence'

defineProps<{ items: RecurrenceDto[]; busy?: boolean }>()
const emit = defineEmits<{
  edit: [rule: RecurrenceDto]
  toggle: [rule: RecurrenceDto]
  occurrences: [rule: RecurrenceDto]
}>()

const isTabletUp = useMediaQuery(BREAKPOINTS.tablet)

function source(rule: RecurrenceDto): string | null {
  return rule.creditCardName ?? rule.accountName
}
</script>

<template>
  <div>
    <DataTable v-if="isTabletUp" :value="items" data-key="id" row-hover class="rec-list__table">
      <Column header="Descrição">
        <template #body="{ data }: { data: RecurrenceDto }">
          <button type="button" class="rec-list__link" @click="emit('edit', data)">{{ data.description }}</button>
          <small class="rec-list__muted"
            >{{ data.categoryName }}<template v-if="source(data)"> · {{ source(data) }}</template></small
          >
        </template>
      </Column>
      <Column header="Valor" header-class="rec-list__right" body-class="rec-list__right">
        <template #body="{ data }: { data: RecurrenceDto }">
          <TransactionAmount :amount="data.amount" :type="data.type" />
        </template>
      </Column>
      <Column header="Frequência">
        <template #body="{ data }: { data: RecurrenceDto }">{{
          frequencyLabel(data.frequency, data.interval)
        }}</template>
      </Column>
      <Column header="Próxima ocorrência">
        <template #body="{ data }: { data: RecurrenceDto }">{{ formatDate(data.nextOccurrenceDate) }}</template>
      </Column>
      <Column header="Geradas" header-class="rec-list__right" body-class="rec-list__right">
        <template #body="{ data }: { data: RecurrenceDto }">{{ data.generatedCount }}</template>
      </Column>
      <Column header="Situação">
        <template #body="{ data }: { data: RecurrenceDto }">
          <ToneChip
            :label="data.isActive ? 'Ativa' : 'Inativa'"
            :icon="data.isActive ? 'pi pi-check-circle' : 'pi pi-ban'"
            :tone="data.isActive ? 'success' : 'neutral'"
          />
        </template>
      </Column>
      <Column header="Ações" body-class="rec-list__actions">
        <template #body="{ data }: { data: RecurrenceDto }">
          <div class="rec-list__buttons">
            <Button
              type="button"
              label="Ocorrências"
              icon="pi pi-list"
              size="small"
              severity="secondary"
              text
              :aria-label="`Ver ocorrências de ${data.description}`"
              @click="emit('occurrences', data)"
            />
            <Button
              type="button"
              label="Editar"
              icon="pi pi-pencil"
              size="small"
              severity="secondary"
              text
              :aria-label="`Editar ${data.description}`"
              :disabled="busy"
              @click="emit('edit', data)"
            />
            <Button
              type="button"
              :label="data.isActive ? 'Desativar' : 'Ativar'"
              :icon="data.isActive ? 'pi pi-ban' : 'pi pi-replay'"
              size="small"
              :severity="data.isActive ? 'danger' : 'secondary'"
              text
              :aria-label="`${data.isActive ? 'Desativar' : 'Ativar'} ${data.description}`"
              :disabled="busy"
              @click="emit('toggle', data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>

    <ul v-else class="rec-cards" aria-label="Recorrências">
      <li v-for="rule in items" :key="rule.id" class="rec-card" :class="{ 'rec-card--inactive': !rule.isActive }">
        <div class="rec-card__row">
          <span class="rec-card__title">{{ rule.description }}</span>
          <TransactionAmount :amount="rule.amount" :type="rule.type" />
        </div>
        <div class="rec-card__row">
          <span>{{ frequencyLabel(rule.frequency, rule.interval) }}</span>
          <ToneChip
            :label="rule.isActive ? 'Ativa' : 'Inativa'"
            :icon="rule.isActive ? 'pi pi-check-circle' : 'pi pi-ban'"
            :tone="rule.isActive ? 'success' : 'neutral'"
          />
        </div>
        <p class="rec-card__meta">
          <span>Próxima: {{ formatDate(rule.nextOccurrenceDate) }}</span>
          <span>Geradas: {{ rule.generatedCount }}</span>
          <span>{{ rule.categoryName }}</span>
          <span v-if="source(rule)">{{ source(rule) }}</span>
        </p>
        <div class="rec-list__buttons">
          <Button
            type="button"
            label="Ocorrências"
            icon="pi pi-list"
            size="small"
            severity="secondary"
            text
            :aria-label="`Ver ocorrências de ${rule.description}`"
            @click="emit('occurrences', rule)"
          />
          <Button
            type="button"
            label="Editar"
            icon="pi pi-pencil"
            size="small"
            severity="secondary"
            text
            :aria-label="`Editar ${rule.description}`"
            :disabled="busy"
            @click="emit('edit', rule)"
          />
          <Button
            type="button"
            :label="rule.isActive ? 'Desativar' : 'Ativar'"
            :icon="rule.isActive ? 'pi pi-ban' : 'pi pi-replay'"
            size="small"
            :severity="rule.isActive ? 'danger' : 'secondary'"
            text
            :aria-label="`${rule.isActive ? 'Desativar' : 'Ativar'} ${rule.description}`"
            :disabled="busy"
            @click="emit('toggle', rule)"
          />
        </div>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.rec-list__link {
  display: block;
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  font-weight: 600;
  text-align: left;
  cursor: pointer;
}

.rec-list__link:hover {
  text-decoration: underline;
}

.rec-list__muted {
  color: var(--gf-text-muted);
}

.rec-list__buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}

:deep(.rec-list__right) {
  text-align: right;
}

:deep(.rec-list__right .p-datatable-column-header-content) {
  justify-content: flex-end;
}

.rec-cards {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.rec-card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.875rem 1rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.rec-card--inactive {
  border-style: dashed;
}

.rec-card__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
}

.rec-card__title {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.rec-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1rem;
  margin: 0;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}
</style>
