<script setup lang="ts">
import { computed, ref } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import TypeToggle from '@/modules/reports/components/TypeToggle.vue'
import { colorFor, describeShares, donutOption } from '@/modules/reports/charts'
import type { TransactionType } from '@/types/api'
import { formatCurrency, formatPercent } from '@/utils/format'
import { displayName, PARTICIPATION_COPY, participationRows, type ParticipationKind } from '../dashboard'
import type { GroupParticipationItemDto } from '../types'

const props = defineProps<{ kind: ParticipationKind; items: GroupParticipationItemDto[] }>()

const type = ref<TransactionType>('Expense')
const copy = computed(() => PARTICIPATION_COPY[props.kind])
const titleId = computed(() => `participation-${props.kind}-title`)
const ruleId = computed(() => `participation-${props.kind}-rule`)

const rows = computed(() =>
  participationRows(props.items, type.value).map((row, index) => ({ ...row, color: colorFor(index) })),
)
const typeLabel = computed(() => (type.value === 'Expense' ? 'despesas' : 'receitas'))
const option = computed(() => donutOption(rows.value.map((r) => ({ name: r.name, value: r.amount, color: r.color }))))
const ariaLabel = computed(() =>
  describeShares(
    `${copy.value.title} (${typeLabel.value})`,
    rows.value.map((r) => ({ name: r.name, total: r.amount, percentage: r.percent })),
  ),
)

const tableRows = computed(() => props.items.map((item) => ({ ...item, label: displayName(item) })))
const verb = computed(() => (props.kind === 'createdBy' ? 'cadastrado' : 'pago'))
</script>

<template>
  <section class="surface-card participation" :aria-labelledby="titleId" :aria-describedby="ruleId">
    <div class="report-header">
      <h3 :id="titleId" class="report-title">{{ copy.title }}</h3>
      <TypeToggle v-model="type" />
    </div>
    <p :id="ruleId" class="participation__rule" data-testid="participation-rule">{{ copy.rule }}</p>

    <ChartPanel
      :option="option"
      v-bind="{ ariaLabel }"
      height="260px"
      :empty="rows.length === 0"
      :empty-message="`Nenhum valor de ${typeLabel} ${verb} no período.`"
    >
      <template #table>
        <div class="table-scroll">
          <table class="simple-table" :data-testid="`participation-table-${kind}`">
            <caption class="sr-only">
              {{
                copy.title
              }}
              — valores e percentuais de despesas e receitas
            </caption>
            <thead>
              <tr>
                <th scope="col">Participante</th>
                <th scope="col" class="num">Despesas</th>
                <th scope="col" class="num">% despesas</th>
                <th scope="col" class="num">Receitas</th>
                <th scope="col" class="num">% receitas</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in tableRows" :key="row.userId">
                <th scope="row">{{ row.label }}</th>
                <td class="num money">{{ formatCurrency(row.expenseAmount) }}</td>
                <td class="num">{{ formatPercent(row.expensePercent) }}</td>
                <td class="num money">{{ formatCurrency(row.incomeAmount) }}</td>
                <td class="num">{{ formatPercent(row.incomePercent) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </ChartPanel>
  </section>
</template>

<style scoped>
.participation__rule {
  margin: 0 0 1rem;
  font-size: 0.875rem;
  color: var(--gf-text-muted);
}
</style>
