<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import AppNav from '@/components/layout/AppNav.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import UserMenu from '@/components/layout/UserMenu.vue'
import { BREAKPOINTS, useMediaQuery } from '@/composables/useMediaQuery'
import NotificationBell from '@/modules/notifications/components/NotificationBell.vue'
import { useNotificationRefresh } from '@/modules/notifications/composables'
import { NAV_ITEMS } from './navigation'

const route = useRoute()
useNotificationRefresh()
const isTabletUp = useMediaQuery(BREAKPOINTS.tablet)
const isDesktop = useMediaQuery(BREAKPOINTS.desktop)

/** Tablet: sidebar recolhida (ícones) por padrão, expansível. Desktop: sempre expandida. */
const tabletExpanded = ref(false)
const drawerOpen = ref(false)

const sidebarCompact = computed(() => isTabletUp.value && !isDesktop.value && !tabletExpanded.value)

watch(
  () => route.fullPath,
  () => {
    drawerOpen.value = false
    tabletExpanded.value = false
  },
)

watch(isTabletUp, (tabletUp) => {
  if (tabletUp) drawerOpen.value = false
})
</script>

<template>
  <a class="skip-link" href="#main-content">Pular para o conteúdo</a>
  <div class="app-shell" :class="{ 'app-shell--expanded': tabletExpanded }">
    <aside class="app-sidebar" :class="{ 'app-sidebar--compact': sidebarCompact }">
      <div class="app-sidebar__brand">
        <RouterLink :to="{ name: 'dashboard' }" class="app-sidebar__home" aria-label="GroupFinance — início">
          <BrandLogo :compact="sidebarCompact" />
        </RouterLink>
      </div>
      <nav class="app-sidebar__nav" aria-label="Navegação principal">
        <AppNav :items="NAV_ITEMS" :compact="sidebarCompact" />
      </nav>
      <div class="app-sidebar__footer">
        <Button
          class="app-sidebar__collapse"
          :icon="tabletExpanded ? 'pi pi-angle-double-left' : 'pi pi-angle-double-right'"
          :aria-label="tabletExpanded ? 'Recolher menu lateral' : 'Expandir menu lateral'"
          :aria-expanded="tabletExpanded"
          severity="secondary"
          text
          rounded
          @click="tabletExpanded = !tabletExpanded"
        />
      </div>
    </aside>

    <div class="app-main">
      <header class="app-topbar">
        <Button
          class="app-topbar__menu"
          icon="pi pi-bars"
          aria-label="Abrir menu de navegação"
          aria-controls="mobile-nav"
          :aria-expanded="drawerOpen"
          severity="secondary"
          text
          rounded
          @click="drawerOpen = true"
        />
        <RouterLink :to="{ name: 'dashboard' }" class="app-topbar__brand" aria-label="GroupFinance — início">
          <BrandLogo />
        </RouterLink>
        <div class="app-topbar__spacer" />
        <NotificationBell />
        <ThemeToggle />
        <UserMenu />
      </header>

      <main id="main-content" class="app-content" tabindex="-1">
        <RouterView />
      </main>
    </div>

    <Drawer v-model:visible="drawerOpen" header="Menu" position="left" class="app-drawer" block-scroll>
      <nav id="mobile-nav" aria-label="Navegação principal (menu móvel)">
        <AppNav :items="NAV_ITEMS" @navigate="drawerOpen = false" />
      </nav>
    </Drawer>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
}

.app-sidebar {
  display: none;
}

.app-main {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.app-topbar {
  position: sticky;
  top: 0;
  z-index: 100;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  height: var(--gf-topbar-height);
  padding: 0 1rem;
  background: var(--gf-surface);
  border-bottom: 1px solid var(--gf-border);
}

.app-topbar > :not(.app-topbar__spacer) {
  /* Nenhum botão (menu, notificações, tema, usuário) pode ser espremido em telas estreitas. */
  flex-shrink: 0;
}

.app-topbar__brand {
  min-width: 0;
  text-decoration: none;
}

/* Telas muito estreitas (ex.: 360 px): só o ícone da marca, para o menu e as ações caberem. */
@media (max-width: 479px) {
  .app-topbar__brand :deep(.brand__name) {
    display: none;
  }
}

.app-topbar__spacer {
  flex: 1;
}

.app-content {
  flex: 1;
  width: 100%;
  max-width: 1280px;
  margin: 0 auto;
  padding: 1.25rem 1rem 2rem;
}

.app-content:focus {
  outline: none;
}

/* Tablet e acima: sidebar fixa */
@media (min-width: 768px) {
  .app-sidebar {
    position: fixed;
    inset: 0 auto 0 0;
    z-index: 200;
    display: flex;
    flex-direction: column;
    width: var(--gf-sidebar-collapsed-width);
    background: var(--gf-surface);
    border-right: 1px solid var(--gf-border);
    transition: width 0.2s ease;
  }

  .app-shell--expanded .app-sidebar {
    width: var(--gf-sidebar-width);
    box-shadow: 0 0 24px rgb(0 0 0 / 0.12);
  }

  .app-sidebar__brand {
    display: flex;
    align-items: center;
    height: var(--gf-topbar-height);
    padding: 0 1rem;
    border-bottom: 1px solid var(--gf-border);
  }

  .app-sidebar__home {
    text-decoration: none;
  }

  .app-sidebar__nav {
    flex: 1;
    overflow-y: auto;
    padding: 0.75rem;
  }

  .app-sidebar__footer {
    display: flex;
    justify-content: center;
    padding: 0.5rem;
    border-top: 1px solid var(--gf-border);
  }

  .app-main {
    margin-left: var(--gf-sidebar-collapsed-width);
  }

  .app-topbar__menu,
  .app-topbar__brand {
    display: none;
  }

  .app-content {
    padding: 1.5rem 1.5rem 2.5rem;
  }
}

/* Desktop: sidebar sempre expandida, sem botão de recolher */
@media (min-width: 1024px) {
  .app-sidebar,
  .app-shell--expanded .app-sidebar {
    width: var(--gf-sidebar-width);
    box-shadow: none;
  }

  .app-sidebar__footer {
    display: none;
  }

  .app-main {
    margin-left: var(--gf-sidebar-width);
  }

  .app-content {
    padding: 2rem 2rem 3rem;
  }
}
</style>
