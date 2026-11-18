<script setup lang="ts">
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'

/**
 * Diálogo de confirmação padronizado. Para ações destrutivas use `severity="danger"`.
 * O slot padrão permite conteúdo adicional (ex.: campo de senha de confirmação).
 */
withDefaults(
  defineProps<{
    header: string
    message?: string
    confirmLabel?: string
    cancelLabel?: string
    severity?: 'primary' | 'danger'
    loading?: boolean
    confirmDisabled?: boolean
  }>(),
  {
    message: undefined,
    confirmLabel: 'Confirmar',
    cancelLabel: 'Cancelar',
    severity: 'primary',
    loading: false,
    confirmDisabled: false,
  },
)

const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ confirm: []; cancel: [] }>()

// O evento `cancel` é emitido no `@hide` (botão Cancelar, Esc ou fechar).
function cancel(): void {
  visible.value = false
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="header"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(32rem, calc(100vw - 2rem))' }"
    @hide="emit('cancel')"
  >
    <form class="confirm-dialog" novalidate @submit.prevent="emit('confirm')">
      <p v-if="message" class="confirm-dialog__message">
        <i v-if="severity === 'danger'" class="pi pi-exclamation-triangle" aria-hidden="true" />
        {{ message }}
      </p>
      <slot />
      <div class="form-actions">
        <Button type="button" :label="cancelLabel" severity="secondary" text :disabled="loading" @click="cancel" />
        <Button
          type="submit"
          :label="confirmLabel"
          :severity="severity === 'danger' ? 'danger' : undefined"
          :loading="loading"
          :disabled="confirmDisabled"
        />
      </div>
    </form>
  </Dialog>
</template>

<style scoped>
.confirm-dialog {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.confirm-dialog__message {
  margin: 0;
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
}

.confirm-dialog__message .pi {
  color: var(--gf-danger);
  margin-top: 0.2rem;
}
</style>
