<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { toAppError } from '@/utils/problem'
import { groupService } from '../services/groupService'
import type { GroupDetailDto, GroupRequest } from '../types'
import { GROUP_FORM_FIELDS } from '../validation'
import GroupForm from './GroupForm.vue'

const props = withDefaults(defineProps<{ group?: GroupDetailDto | null }>(), { group: null })
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [group: GroupDetailDto] }>()

useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
const formKey = ref(0)

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
  }
})

async function onSubmit(request: GroupRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const saved = props.group ? await groupService.update(props.group.id, request) : await groupService.create(request)
    visible.value = false
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (GROUP_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    errorMessage.value = mapped ? null : appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="group ? 'Editar grupo' : 'Novo grupo'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(32rem, calc(100vw - 1.5rem))' }"
  >
    <GroupForm
      :key="formKey"
      id-prefix="group-dialog"
      :initial="group"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      :submit-label="group ? 'Salvar alterações' : 'Criar grupo'"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
