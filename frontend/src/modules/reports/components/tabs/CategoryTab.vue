<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import type { TransactionType } from '@/types/api'
import { formatCurrency, formatPercent } from '@/utils/format'
import { colorFor, describeShares, donutOption } from '../../charts'
import { useReport } from '../../composables'
import { reportService } from '../../services/reportService'
import ReportState from '../ReportState.vue'
import TypeToggle from '../TypeToggle.vue'

const props = defineProps<{ from: string; to: string }>()
const type = ref<TransactionType>('Expense')
const expanded = ref<Set<string>>(new Set())

const report = useReport(
  () => reportService.byCategory({ from: props.from, to: props.to, type: type.value }),
  () => `${props.from}|${props.to}|${type.value}`,
)
const data = report.data

watch(
  () => `${props.from}|${props.to}|${type.value}`,
  () => {
    expanded.value = new Set()
  },
)

const rows = computed(() =>
  (data.value?.items ?? []).map((item, i) => ({ ...item, displayColor: colorFor(i, item.color) })),
)
const title = computed(() => `${type.value === 'Expense' ? 'Despesas' : 'Receitas'} por categoria`)
const option = computed(() =>
  donutOption(rows.value.map((r) => ({ name: r.categoryName, value: r.total, color: r.displayColor }))),
)
const ariaLabel = computed(() =>
  describeShares(
    title.value,
    rows.value.map((r) => ({ name: r.categoryName, total: r.total, percentage: r.percentage })),
  ),
)

function toggle(id: string): void {
  const next = new Set(expanded.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expanded.value = next
}
</script>

<template>
  <section class="surface-card" aria-labelledby="by-category-title">
    <div class="report-header">
      <h2 id="by-category-title" class="report-title">{{ title }}</h2>
      <TypeToggle v-model="type" />
    </div>
    <ReportState
      :loading="report.loading.value"
      :error="report.error.value"
      :loaded="data !== null"
      :empty="rows.length === 0"
      empty-title="Nenhuma movimentação no período"
      @retry="report.reload()"
    >
      <ChartPanel v-if="data" :option="option" v-bind="{ ariaLabel }" table-open height="280px">
        <template #table>
          <table class="simple-table" data-testid="category-table">
            <caption class="sr-only">
              {{
                title
              }}
              — use os botões para expandir as subcategorias
            </caption>
            <thead>
              <tr>
                <th scope="col">Categoria</th>
                <th scope="col" class="num">Total</th>
                <th scope="col" class="num">%</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="row in rows" :key="row.categoryId">
                <tr>
                  <th scope="row">
                    <span class="cat-dot" :style="{ background: row.displayColor }" aria-hidden="true" />
                    <Button
                      v-if="row.subcategories.length > 0"
                      type="button"
                      :icon="expanded.has(row.categoryId) ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
                      severity="secondary"
                      text
                      rounded
                      size="small"
                      :aria-expanded="expanded.has(row.categoryId)"
                      :aria-label="`${expanded.has(row.categoryId) ? 'Recolher' : 'Expandir'} subcategorias de ${row.categoryName}`"
                      @click="toggle(row.categoryId)"
                    />
                    {{ row.categoryName }}
                  </th>
                  <td class="num money">{{ formatCurrency(row.total) }}</td>
                  <td class="num">{{ formatPercent(row.percentage) }}</td>
                </tr>
                <tr
                  v-for="sub in expanded.has(row.categoryId) ? row.subcategories : []"
                  :key="sub.categoryId"
                  class="cat-sub"
                  data-testid="subcategory-row"
                >
                  <th scope="row" class="cat-sub__name">{{ sub.categoryName }}</th>
                  <td class="num money">{{ formatCurrency(sub.total) }}</td>
                  <td class="num" :title="`Percentual em relação a ${row.categoryName}`">
                    {{ formatPercent(sub.percentage) }}
                    <span class="sr-only"> de {{ row.categoryName }}</span>
                  </td>
                </tr>
              </template>
            </tbody>
            <tfoot>
              <tr>
                <th scope="row">Total</th>
                <td class="num money">{{ formatCurrency(data.total) }}</td>
                <td />
              </tr>
            </tfoot>
          </table>
          <p class="report-note">O percentual das subcategorias é relativo ao total da categoria principal.</p>
        </template>
      </ChartPanel>
    </ReportState>
  </section>
</template>

<style scoped>
.cat-dot {
  display: inline-block;
  width: 0.75rem;
  height: 0.75rem;
  margin-right: 0.375rem;
  border-radius: 50%;
}

.cat-sub__name {
  padding-left: 2.75rem;
  color: var(--gf-text-muted);
}
</style>
