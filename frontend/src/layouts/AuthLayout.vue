<script setup lang="ts">
import BrandLogo from '@/components/layout/BrandLogo.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
</script>

<template>
  <a class="skip-link" href="#main-content">Pular para o conteúdo</a>
  <div class="auth-layout">
    <header class="auth-layout__header">
      <BrandLogo />
      <ThemeToggle />
    </header>
    <main id="main-content" class="auth-layout__main" tabindex="-1">
      <div class="auth-layout__card">
        <RouterView />
      </div>
    </main>
    <footer class="auth-layout__footer">
      <small>Seus dados financeiros individuais são privados.</small>
    </footer>
  </div>
</template>

<style scoped>
.auth-layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background:
    radial-gradient(circle at 15% 10%, color-mix(in srgb, var(--p-primary-color) 14%, transparent), transparent 45%),
    var(--gf-bg);
}

.auth-layout__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.25rem;
}

.auth-layout__main {
  flex: 1;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 1rem;
}

.auth-layout__main:focus {
  outline: none;
}

.auth-layout__card {
  width: 100%;
  max-width: 28rem;
  background: var(--gf-surface);
  border: 1px solid var(--gf-border);
  border-radius: 16px;
  padding: 2rem 1.5rem;
  box-shadow: 0 10px 30px rgb(0 0 0 / 0.06);
}

@media (min-width: 768px) {
  .auth-layout__main {
    align-items: center;
  }

  .auth-layout__card {
    padding: 2.5rem;
  }
}

.auth-layout__footer {
  text-align: center;
  padding: 1rem;
  color: var(--gf-text-muted);
}
</style>
