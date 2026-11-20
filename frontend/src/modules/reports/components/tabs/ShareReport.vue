<script setup lang="ts">
import { computed } from 'vue'
import ChartPanel from '@/components/charts/ChartPanel.vue'
import { formatCurrency, formatPercent } from '@/utils/format'
import { colorFor, describeShares, donutOption } from '../../charts'

export interface ShareRow {
  key: string
  label: string
  total: number
  percentage: number
  color?: string | null
}

/** Rosca de participação + tabela (alternativa acessível) para relatórios "por conta" e "por método". */
const props = defineProps<{ rows: ShareRow[]; total: number; title: string; column: string }>()

const colored = computed(() => props.rows.map((r, i) => ({ ...r, displayColor: colorFor(i, r.color) })))
const option = computed(() =>
  donutOption(colored.value.map((r) => ({ name: r.label, value: r.total, color: r.displayColor }))),
)
const ariaLabel = computed(() =>
  describeShares(
    props.title,
    props.rows.map((r) => ({ name: r.label, total: r.total, percentage: r.percentage })),
  ),
)
</script>

<template>
  <ChartPanel :option="option" v-bind="{ ariaLabel }" :empty="rows.length === 0" table-open height="280px">
    <template #table>
      <table class="simple-table" data-testid="share-table">
        <caption class="sr-only">
          {{
            title
          }}
        </caption>
        <thead>
          <tr>
            <th scope="col">{{ column }}</th>
            <th scope="col" class="num">Total</th>
            <th scope="col" class="num">%</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in colored" :key="row.key">
            <th scope="row">
              <span class="share-dot" :style="{ background: row.displayColor }" aria-hidden="true" />{{ row.label }}
            </th>
            <td class="num money">{{ formatCurrency(row.total) }}</td>
            <td class="num">{{ formatPercent(row.percentage) }}</td>
          </tr>
        </tbody>
        <tfoot>
          <tr>
            <th scope="row">Total</th>
            <td class="num money">{{ formatCurrency(total) }}</td>
            <td />
          </tr>
        </tfoot>
      </table>
    </template>
  </ChartPanel>
</template>

<style scoped>
.share-dot {
  display: inline-block;
  width: 0.75rem;
  height: 0.75rem;
  margin-right: 0.5rem;
  border-radius: 50%;
}
</style>
