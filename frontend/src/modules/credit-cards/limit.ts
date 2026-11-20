import { ratioPercent } from '@/utils/format'

export type LimitState = 'ok' | 'high' | 'exceeded' | 'unknown'

export interface LimitPresentation {
  /** Percentual utilizado (pode passar de 100); `null` com limite zero. */
  percent: number | null
  /** Valor da barra, limitado a 0–100. */
  barValue: number
  state: LimitState
}

/** Percentual alto a partir do qual a barra muda de aparência (apenas apresentação). */
export const HIGH_USAGE_PERCENT = 80

/**
 * Apresentação do limite: usa `availableLimit` devolvido pela API (negativo = excedido).
 * Nenhum valor financeiro é calculado aqui — apenas a proporção visual.
 */
export function limitPresentation(usedLimit: number, creditLimit: number, availableLimit: number): LimitPresentation {
  const percent = ratioPercent(Math.max(usedLimit, 0), creditLimit)
  const barValue = percent === null ? (usedLimit > 0 ? 100 : 0) : Math.min(100, Math.max(0, percent))
  let state: LimitState = 'ok'
  if (availableLimit < 0) state = 'exceeded'
  else if (percent === null) state = usedLimit > 0 ? 'exceeded' : 'unknown'
  else if (percent >= HIGH_USAGE_PERCENT) state = 'high'
  return { percent, barValue, state }
}
