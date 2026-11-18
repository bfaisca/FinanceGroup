import { onBeforeUnmount, onMounted, ref, type Ref } from 'vue'

/** Breakpoints do layout (CLAUDE.md §14): mobile < 768 ≤ tablet < 1024 ≤ desktop. */
export const BREAKPOINTS = {
  tablet: '(min-width: 768px)',
  desktop: '(min-width: 1024px)',
} as const

export function useMediaQuery(query: string): Ref<boolean> {
  const matches = ref(false)
  let mql: MediaQueryList | null = null

  const update = (event: MediaQueryList | MediaQueryListEvent) => {
    matches.value = event.matches
  }

  onMounted(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return
    mql = window.matchMedia(query)
    update(mql)
    mql.addEventListener('change', update)
  })

  onBeforeUnmount(() => {
    mql?.removeEventListener('change', update)
  })

  return matches
}
