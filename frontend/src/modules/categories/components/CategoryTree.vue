<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import CategoryBadge from '@/components/CategoryBadge.vue'
import type { CategoryDto, CategoryType } from '@/types/api'
import { buildCategoryTree, type CategoryNode } from '../tree'
import { canAddSubcategory, canModifyCategory } from '../validation'

const props = defineProps<{
  items: CategoryDto[]
  type: CategoryType
  busy?: boolean
}>()

const emit = defineEmits<{
  'add-sub': [parent: CategoryDto]
  edit: [category: CategoryDto]
  toggle: [category: CategoryDto]
}>()

const nodes = computed<CategoryNode[]>(() => buildCategoryTree(props.items, props.type))
</script>

<template>
  <ul class="category-tree" aria-label="Categorias">
    <li v-for="node in nodes" :key="node.category.id" class="category-tree__node">
      <div class="category-tree__row" :class="{ 'category-tree__row--inactive': !node.category.isActive }">
        <CategoryBadge :name="node.category.name" :color="node.category.color" :icon="node.category.icon" />
        <span v-if="node.category.isDefault" class="category-tree__badge">Padrão</span>
        <span v-if="!node.category.isActive" class="category-tree__badge category-tree__badge--inactive">Inativa</span>
        <span class="category-tree__spacer" />
        <div class="category-tree__actions">
          <Button
            v-if="canAddSubcategory(node.category)"
            type="button"
            icon="pi pi-plus"
            label="Subcategoria"
            size="small"
            severity="secondary"
            text
            :aria-label="`Adicionar subcategoria em ${node.category.name}`"
            :disabled="busy"
            @click="emit('add-sub', node.category)"
          />
          <template v-if="canModifyCategory(node.category)">
            <Button
              type="button"
              icon="pi pi-pencil"
              size="small"
              severity="secondary"
              text
              rounded
              :aria-label="`Editar ${node.category.name}`"
              :disabled="busy"
              @click="emit('edit', node.category)"
            />
            <Button
              type="button"
              :icon="node.category.isActive ? 'pi pi-ban' : 'pi pi-replay'"
              size="small"
              :severity="node.category.isActive ? 'danger' : 'secondary'"
              text
              rounded
              :aria-label="`${node.category.isActive ? 'Desativar' : 'Reativar'} ${node.category.name}`"
              :disabled="busy"
              @click="emit('toggle', node.category)"
            />
          </template>
        </div>
      </div>

      <ul
        v-if="node.children.length > 0"
        class="category-tree__children"
        :aria-label="`Subcategorias de ${node.category.name}`"
      >
        <li
          v-for="child in node.children"
          :key="child.id"
          class="category-tree__row"
          :class="{ 'category-tree__row--inactive': !child.isActive }"
        >
          <CategoryBadge
            :name="child.name"
            :color="child.color ?? node.category.color"
            :icon="child.icon ?? node.category.icon"
          />
          <span v-if="child.isDefault" class="category-tree__badge">Padrão</span>
          <span v-if="!child.isActive" class="category-tree__badge category-tree__badge--inactive">Inativa</span>
          <span class="category-tree__spacer" />
          <div v-if="canModifyCategory(child)" class="category-tree__actions">
            <Button
              type="button"
              icon="pi pi-pencil"
              size="small"
              severity="secondary"
              text
              rounded
              :aria-label="`Editar ${child.name}`"
              :disabled="busy"
              @click="emit('edit', child)"
            />
            <Button
              type="button"
              :icon="child.isActive ? 'pi pi-ban' : 'pi pi-replay'"
              size="small"
              :severity="child.isActive ? 'danger' : 'secondary'"
              text
              rounded
              :aria-label="`${child.isActive ? 'Desativar' : 'Reativar'} ${child.name}`"
              :disabled="busy"
              @click="emit('toggle', child)"
            />
          </div>
        </li>
      </ul>
    </li>
  </ul>
</template>

<style scoped>
.category-tree,
.category-tree__children {
  list-style: none;
  margin: 0;
  padding: 0;
}

.category-tree {
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.category-tree__node + .category-tree__node {
  border-top: 1px solid var(--gf-border);
}

.category-tree__row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  min-height: 3rem;
}

.category-tree__row--inactive {
  opacity: 0.75;
}

.category-tree__children {
  padding-left: 1.75rem;
  border-left: 2px solid var(--gf-border);
  margin: 0 0 0.5rem 1.25rem;
}

.category-tree__spacer {
  flex: 1;
}

.category-tree__actions {
  display: flex;
  align-items: center;
  gap: 0.125rem;
}

.category-tree__badge {
  padding: 0.0625rem 0.5rem;
  border-radius: 999px;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-size: 0.75rem;
  font-weight: 600;
}

.category-tree__badge--inactive {
  background: color-mix(in srgb, var(--gf-neutral) 14%, transparent);
  color: var(--gf-neutral);
}
</style>
