<script setup lang="ts">
import { computed } from 'vue'
import MonthsSelect from '@/components/MonthsSelect.vue'
import CommitmentSection from '@/modules/installments/components/CommitmentSection.vue'
import InstallmentStatusChip from '@/modules/installments/components/InstallmentStatusChip.vue'
import { formatCurrency, formatDate } from '@/utils/format'
import { useReport } from '../../composables'
import { reportService } from '../../services/reportService'
import ReportState from '../ReportState.vue'

const props = defineProps<{ months: number }>()
const emit = defineEmits<{ 'update:months': [months: number] }>()

const report = useReport(
  () => reportService.installments({ months: props.months }),
  () => String(props.months),
)
const data = report.data
const isEmpty = computed(() => data.value !== null && data.value.openPlans === 0 && data.value.plans.length === 0)
</script>

<template>
  <section class="surface-card" aria-labelledby="inst-report-title">
    <div class="report-header">
      <h2 id="inst-report-title" class="report-title">Parcelamentos</h2>
      <MonthsSelect
        :model-value="months"
        label="Janela"
        aria-label="Meses do comprometimento"
        @update:model-value="emit('update:months', $event)"
      />
    </div>
    <ReportState
      :loading="report.loading.value"
      :error="report.error.value"
      :loaded="data !== null"
      :empty="isEmpty"
      empty-title="Nenhum parcelamento em aberto"
      empty-description="Quando houver compras parceladas em aberto, o comprometimento futuro aparece aqui."
      @retry="report.reload()"
    >
      <div v-if="data" class="stack">
        <CommitmentSection :report="data" />

        <section aria-labelledby="open-plans-title">
          <h3 id="open-plans-title" class="report-title">Planos em aberto</h3>
          <div class="table-scroll">
            <table class="simple-table" data-testid="open-plans-table">
              <caption class="sr-only">
                Planos de parcelamento em aberto, por próximo vencimento
              </caption>
              <thead>
                <tr>
                  <th scope="col">Plano</th>
                  <th scope="col">Situação</th>
                  <th scope="col" class="num">Pagas</th>
                  <th scope="col" class="num">Restante</th>
                  <th scope="col">Próx. vencimento</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="plan in data.plans" :key="plan.id">
                  <th scope="row">
                    <RouterLink :to="{ name: 'installments', query: { open: plan.id } }">{{
                      plan.description
                    }}</RouterLink>
                  </th>
                  <td><InstallmentStatusChip :status="plan.status" /></td>
                  <td class="num">{{ plan.paidCount }}/{{ plan.installmentCount }}</td>
                  <td class="num money">{{ formatCurrency(plan.remainingAmount) }}</td>
                  <td>{{ formatDate(plan.nextDueDate) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </ReportState>
  </section>
</template>
