import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { DateOnlyString } from '@/types/api'
import { localToday } from '@/utils/period'

/**
 * "Hoje" da aplicação (D-021). O backend é a fonte (dashboard `today`, contas `projectedAsOf`);
 * enquanto nada foi devolvido, usa a data local do navegador.
 */
export const useClockStore = defineStore('clock', () => {
  const today = ref<DateOnlyString>(localToday())
  const fromServer = ref(false)

  function setServerToday(value: DateOnlyString): void {
    if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
      today.value = value
      fromServer.value = true
    }
  }

  return { today, fromServer, setServerToday }
})
