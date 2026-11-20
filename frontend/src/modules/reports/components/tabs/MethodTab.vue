<script setup lang="ts">
import { computed, ref } from 'vue'
import type { TransactionType } from '@/types/api'
import { paymentMethodLabel } from '@/utils/labels'
import { useReport } from '../../composables'
import { reportService } from '../../services/reportService'
import ReportState from '../ReportState.vue'
import TypeToggle from '../TypeToggle.vue'
import ShareReport from './ShareReport.vue'

const props = defineProps<{ from: string; to: string }>()
const type = ref<TransactionType>('Expense')

const report = useReport(
  () => reportService.byPaymentMethod({ from: props.from, to: props.to, type: type.value }),
  () => `${props.from}|${props.to}|${type.value}`,
)
const data = report.data

const rows = computed(() =>
  (data.value?.items ?? []).map((i) => ({
    key: i.paymentMethod ?? 'none',
    // `null` = "Não informado" (contrato §14).
    label: paymentMethodLabel(i.paymentMethod),
    total: i.total,
    percentage: i.percentage,
  })),
)
const title = computed(() => `${type.value === 'Expense' ? 'Despesas' : 'Receitas'} por método de pagamento`)
</script>

<template>
  <section class="surface-card" aria-labelledby="by-method-title">
    <div class="report-header">
      <h2 id="by-method-title" class="report-title">{{ title }}</h2>
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
      <ShareReport v-if="data" :rows="rows" :total="data.total" :title="title" column="Método" />
    </ReportState>
  </section>
</template>
