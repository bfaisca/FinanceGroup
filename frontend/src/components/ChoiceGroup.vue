<script setup lang="ts">
/**
 * Grupo de opções (radio) acessível com aparência de amostras. Cada opção é um `<input type="radio">`
 * real (teclado: setas/Tab), com rótulo textual para leitores de tela; a seleção não depende só da cor.
 */
defineProps<{
  legend: string
  name: string
  options: readonly { value: string; label: string }[]
  /** `style` de cada amostra (ex.: cor de fundo) ou ícone. */
  kind: 'color' | 'icon'
  noneLabel?: string
}>()

const model = defineModel<string | null>({ required: true })
</script>

<template>
  <fieldset class="choice-group">
    <legend>{{ legend }}</legend>
    <div class="choice-group__options">
      <label
        v-if="noneLabel"
        class="choice-group__option"
        :class="{ 'choice-group__option--selected': model === null }"
      >
        <input v-model="model" type="radio" :name="name" :value="null" class="sr-only" />
        <span class="choice-group__swatch choice-group__swatch--none" aria-hidden="true"
          ><i class="pi pi-times"
        /></span>
        <span class="sr-only">{{ noneLabel }}</span>
      </label>
      <label
        v-for="option in options"
        :key="option.value"
        class="choice-group__option"
        :class="{ 'choice-group__option--selected': model === option.value }"
      >
        <input v-model="model" type="radio" :name="name" :value="option.value" class="sr-only" />
        <span
          class="choice-group__swatch"
          :style="kind === 'color' ? { background: option.value } : undefined"
          aria-hidden="true"
        >
          <i v-if="kind === 'icon'" :class="['pi', option.value]" />
          <i v-else-if="model === option.value" class="pi pi-check choice-group__check" />
        </span>
        <span class="sr-only">{{ option.label }}</span>
      </label>
    </div>
  </fieldset>
</template>

<style scoped>
.choice-group {
  margin: 0;
  padding: 0;
  border: 0;
}

.choice-group legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 500;
  font-size: 0.9375rem;
}

.choice-group__options {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.choice-group__option {
  position: relative;
  display: inline-flex;
  cursor: pointer;
}

.choice-group__swatch {
  display: grid;
  place-items: center;
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 50%;
  border: 2px solid transparent;
  background: var(--p-content-hover-background);
  color: var(--gf-text);
}

.choice-group__check {
  color: #fff;
  font-size: 0.875rem;
}

.choice-group__option--selected .choice-group__swatch {
  border-color: var(--gf-text);
  box-shadow: 0 0 0 2px var(--gf-surface) inset;
}

.choice-group__option:has(input:focus-visible) .choice-group__swatch {
  outline: var(--gf-focus-ring);
  outline-offset: 2px;
}
</style>
