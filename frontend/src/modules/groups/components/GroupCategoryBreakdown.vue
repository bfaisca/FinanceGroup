<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import { colorFor, describeShares, donutOption } from '@/modules/reports/charts'
import { formatCurrency, formatPercent } from '@/utils/format'
import type { GroupDashboardDto } from '../types'

const props = defineProps<{ items: GroupDashboardDto['expensesByCategory'] }>()

const expanded = ref<Set<string>>(new Set())
const rows = computed(() => props.items.map((item, i) => ({ ...item, displayColor: colorFor(i, item.color) })))
const option = computed(() =>
  donutOption(rows.value.map((r) => ({ name: r.name, value: r.amount, color: r.displayColor }))),
)
const ariaLabel = computed(() =>
  describeShares(
    'Despesas por categoria',
    rows.value.map((r) => ({ name: r.name, total: r.amount, percentage: r.percent })),
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
  <section class="surface-card" aria-labelledby="group-categories-title">
    <h3 id="group-categories-title" class="report-title group-categories__title">Despesas por categoria</h3>
    <ChartPanel
      :option="option"
      v-bind="{ ariaLabel }"
      height="260px"
      :empty="rows.length === 0"
      empty-message="Nenhuma despesa no período."
    >
      <template #table>
        <div class="table-scroll">
          <table class="simple-table" data-testid="group-category-table">
            <caption class="sr-only">
              Despesas por categoria — use os botões para expandir as subcategorias
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
                      v-if="row.children.length > 0"
                      type="button"
                      :icon="expanded.has(row.categoryId) ? 'pi pi-chevron-down' : 'pi pi-chevron-right'"
                      severity="secondary"
                      text
                      rounded
                      size="small"
                      :aria-expanded="expanded.has(row.categoryId)"
                      :aria-label="`${expanded.has(row.categoryId) ? 'Recolher' : 'Expandir'} subcategorias de ${row.name}`"
                      @click="toggle(row.categoryId)"
                    />
                    {{ row.name }}
                  </th>
                  <td class="num money">{{ formatCurrency(row.amount) }}</td>
                  <td class="num">{{ formatPercent(row.percent) }}</td>
                </tr>
                <tr
                  v-for="sub in expanded.has(row.categoryId) ? row.children : []"
                  :key="sub.categoryId"
                  class="cat-sub"
                  data-testid="subcategory-row"
                >
                  <th scope="row" class="cat-sub__name">{{ sub.name }}</th>
                  <td class="num money">{{ formatCurrency(sub.amount) }}</td>
                  <td class="num">
                    {{ formatPercent(sub.percent) }}
                    <span class="sr-only"> de {{ row.name }}</span>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
        <p class="report-note">O percentual das subcategorias é relativo ao total da categoria principal.</p>
      </template>
    </ChartPanel>
  </section>
</template>

<style scoped>
.group-categories__title {
  margin-bottom: 1rem;
}

.cat-dot {
  display: inline-block;
  width: 0.75rem;
  height: 0.75rem;
  margin-right: 0.375rem;
  border-radius: 50%;
}

.cat-sub__name {
  padding-left: 2.25rem;
}
</style>
