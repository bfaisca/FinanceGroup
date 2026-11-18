<script setup lang="ts">
import Button from 'primevue/button'
import EmptyState from './EmptyState.vue'
</script>

<template>
  <main id="main-content" class="not-found">
    <EmptyState
      icon="pi pi-compass"
      title="Página não encontrada"
      description="O endereço acessado não existe ou foi movido."
    >
      <RouterLink v-slot="{ navigate, href }" :to="{ name: 'dashboard' }" custom>
        <Button as="a" :href="href" label="Ir para o início" icon="pi pi-home" @click="navigate" />
      </RouterLink>
    </EmptyState>
  </main>
</template>

<style scoped>
.not-found {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 1.5rem;
}
</style>
