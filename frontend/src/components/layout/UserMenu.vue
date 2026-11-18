<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import Menu from 'primevue/menu'
import type { MenuItem } from 'primevue/menuitem'
import { useToast } from 'primevue/usetoast'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const toast = useToast()
const menu = ref<InstanceType<typeof Menu> | null>(null)
const menuId = 'user-menu'

const displayName = computed(() => auth.user?.name ?? '')
const initials = computed(() => {
  const parts = displayName.value.trim().split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? ''
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : ''
  return (first + last).toUpperCase() || '?'
})

async function signOut(): Promise<void> {
  await auth.logout()
  toast.add({ severity: 'info', summary: 'Sessão encerrada', detail: 'Você saiu da sua conta.', life: 3000 })
  await router.replace({ name: 'login' })
}

const items = computed<MenuItem[]>(() => [
  {
    label: displayName.value,
    items: [
      { label: 'Configurações', icon: 'pi pi-cog', command: () => router.push({ name: 'settings' }) },
      { separator: true },
      { label: 'Sair', icon: 'pi pi-sign-out', command: () => void signOut() },
    ],
  },
])

function toggle(event: Event): void {
  menu.value?.toggle(event)
}
</script>

<template>
  <div class="user-menu">
    <Button
      type="button"
      severity="secondary"
      text
      class="user-menu__trigger"
      aria-haspopup="true"
      :aria-controls="menuId"
      :aria-label="`Menu do usuário: ${displayName}`"
      @click="toggle"
    >
      <span class="user-menu__avatar" aria-hidden="true">{{ initials }}</span>
      <span class="user-menu__name">{{ displayName }}</span>
      <i class="pi pi-angle-down" aria-hidden="true" />
    </Button>
    <Menu :id="menuId" ref="menu" :model="items" popup />
  </div>
</template>

<style scoped>
.user-menu__trigger {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--gf-text);
}

.user-menu__avatar {
  display: grid;
  place-items: center;
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
  font-weight: 700;
  font-size: 0.8125rem;
}

.user-menu__name {
  max-width: 12rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 500;
}

@media (max-width: 639px) {
  .user-menu__name {
    display: none;
  }
}
</style>
