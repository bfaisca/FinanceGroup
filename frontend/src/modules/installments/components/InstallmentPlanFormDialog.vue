<script setup lang="ts">
import { ref, watch } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useClockStore } from '@/stores/clock'
import type { CreateInstallmentPlanRequest, InstallmentPlanDetailDto } from '@/types/api'
import { formatCurrency } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { installmentPlanService } from '../services/installmentPlanService'
import { PLAN_FORM_FIELDS } from '../validation'
import InstallmentList from './InstallmentList.vue'
import InstallmentPlanForm from './InstallmentPlanForm.vue'

const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [plan: InstallmentPlanDetailDto]; 'open-plan': [planId: string] }>()

const clock = useClockStore()
useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
const formKey = ref(0)
/** Resultado devolvido pela API: as parcelas geradas são exibidas como vieram (sem cálculo local). */
const created = ref<InstallmentPlanDetailDto | null>(null)

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
    created.value = null
  }
})

async function onSubmit(request: CreateInstallmentPlanRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    created.value = await installmentPlanService.create(request)
    emit('saved', created.value)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (PLAN_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    errorMessage.value = mapped ? null : appError.message
  } finally {
    loading.value = false
  }
}

function viewPlan(): void {
  if (!created.value) return
  const id = created.value.id
  visible.value = false
  emit('open-plan', id)
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="created ? 'Parcelamento criado' : 'Novo parcelamento'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(44rem, calc(100vw - 1.5rem))' }"
  >
    <div v-if="created" class="stack" data-testid="plan-result">
      <p class="plan-result__summary" role="status">
        <strong>{{ created.description }}</strong>: {{ formatCurrency(created.totalAmount) }} em
        {{ created.installmentCount }} parcelas. Confira abaixo as parcelas geradas pelo sistema.
      </p>
      <InstallmentList :installments="created.installments" caption="Parcelas geradas" :show-actions="false" />
      <div class="form-actions">
        <Button type="button" label="Ver plano" icon="pi pi-eye" severity="secondary" text @click="viewPlan" />
        <Button type="button" label="Concluir" icon="pi pi-check" @click="visible = false" />
      </div>
    </div>
    <InstallmentPlanForm
      v-else
      :key="formKey"
      :today="clock.today"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>

<style scoped>
.plan-result__summary {
  margin: 0;
}
</style>
