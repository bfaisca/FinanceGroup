import { onBeforeUnmount } from 'vue'

/** Executa `callback` após `delay` ms sem novas chamadas; cancela ao desmontar. */
export function useDebouncedCallback<A extends unknown[]>(callback: (...args: A) => void, delay = 300) {
  let timer: ReturnType<typeof setTimeout> | null = null

  function cancel(): void {
    if (timer !== null) {
      clearTimeout(timer)
      timer = null
    }
  }

  function run(...args: A): void {
    cancel()
    timer = setTimeout(() => {
      timer = null
      callback(...args)
    }, delay)
  }

  function flush(...args: A): void {
    cancel()
    callback(...args)
  }

  onBeforeUnmount(cancel)
  return { run, cancel, flush }
}
