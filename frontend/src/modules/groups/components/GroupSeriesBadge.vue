<script setup lang="ts">
import ToneChip from '@/components/ToneChip.vue'
import { seriesBadge } from '../recurrence'
import type { GroupSeriesDto } from '../types'

/** Selo "3/10" da ocorrência na série (D-049); o texto para leitores de tela explica o número. */
defineProps<{ series: Pick<GroupSeriesDto, 'number' | 'total'> }>()
</script>

<template>
  <span class="series-badge" data-testid="series-badge">
    <ToneChip :label="seriesBadge(series)" icon="pi pi-replay" tone="info" aria-hidden="true" />
    <span class="sr-only">Conta {{ series.number }} de {{ series.total }} de uma série recorrente</span>
  </span>
</template>
