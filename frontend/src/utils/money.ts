/**
 * Valores monetários em centavos inteiros: evita acúmulo de erro de ponto flutuante.
 * O contrato usa `number` com 2 casas; a conversão acontece apenas nas bordas (entrada do usuário / request).
 */

/** Limite do contrato: 9.999.999.999.999,99 → em centavos. */
export const MAX_CENTS = 999_999_999_999_999

const MONEY_TEXT = /^-?\d+(?:[.,]\d+)*$/

/** Converte `number` do contrato (2 casas) em centavos inteiros. */
export function toCents(value: number): number {
  return Math.round(value * 100)
}

/** Converte centavos inteiros no `number` com 2 casas do contrato. */
export function fromCents(cents: number): number {
  return Math.round(cents) / 100
}

/**
 * Interpreta o texto digitado (pt-BR) e devolve centavos, ou `null` se inválido/vazio.
 * - Com vírgula: a vírgula é o separador decimal e os pontos são milhares ("1.234,56").
 * - Sem vírgula: um único ponto com 1–2 dígitos depois é decimal ("12.5"); demais pontos são milhares ("1.234").
 * - No máximo 2 casas decimais.
 */
export function parseMoneyToCents(text: string, options: { allowNegative?: boolean } = {}): number | null {
  const raw = text.replace(/\s|R\$/g, '')
  if (raw.length === 0 || !MONEY_TEXT.test(raw)) return null

  const negative = raw.startsWith('-')
  if (negative && !options.allowNegative) return null
  const unsigned = negative ? raw.slice(1) : raw

  let integerPart: string
  let decimalPart = ''

  if (unsigned.includes(',')) {
    const pieces = unsigned.split(',')
    if (pieces.length !== 2) return null
    integerPart = pieces[0] ?? ''
    decimalPart = pieces[1] ?? ''
    if (!/^\d{1,3}(\.\d{3})*$|^\d+$/.test(integerPart)) return null
    integerPart = integerPart.replaceAll('.', '')
  } else if (unsigned.includes('.')) {
    const lastDot = unsigned.lastIndexOf('.')
    const tail = unsigned.slice(lastDot + 1)
    const head = unsigned.slice(0, lastDot)
    if (tail.length === 3) {
      // Separador de milhares: "1.234" ou "1.234.567".
      if (!/^\d{1,3}(\.\d{3})+$/.test(unsigned)) return null
      integerPart = unsigned.replaceAll('.', '')
    } else {
      if (head.includes('.')) return null
      integerPart = head
      decimalPart = tail
    }
  } else {
    integerPart = unsigned
  }

  if (integerPart.length === 0 || decimalPart.length > 2 || (unsigned.includes(',') && decimalPart.length === 0))
    return null
  const cents = Number(integerPart) * 100 + Number(decimalPart.padEnd(2, '0'))
  if (!Number.isSafeInteger(cents) || cents > MAX_CENTS) return null
  return negative ? -cents : cents
}

/** Texto de campo monetário a partir de centavos — ex.: 123456 → "1.234,56" (sem símbolo). */
export function formatMoneyInput(cents: number): string {
  const negative = cents < 0
  const abs = Math.abs(Math.round(cents))
  const integer = Math.floor(abs / 100)
  const decimals = String(abs % 100).padStart(2, '0')
  const grouped = String(integer).replace(/\B(?=(\d{3})+(?!\d))/g, '.')
  return `${negative ? '-' : ''}${grouped},${decimals}`
}

/** Reformata o texto digitado (se válido); caso contrário devolve o texto original. */
export function normalizeMoneyText(text: string, options: { allowNegative?: boolean } = {}): string {
  const cents = parseMoneyToCents(text, options)
  return cents === null ? text : formatMoneyInput(cents)
}
