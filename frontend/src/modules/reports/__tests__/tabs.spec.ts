import { describe, expect, it } from 'vitest'
import { DEFAULT_MONTHS, isReportTab, parseMonths, REPORT_TABS } from '../tabs'
import { colorFor, formatSeriesKey, formatSeriesKeyLong } from '../charts'

describe('tabs', () => {
  it('reconhece apenas abas válidas', () => {
    for (const t of REPORT_TABS) expect(isReportTab(t.value)).toBe(true)
    expect(isReportTab('groups')).toBe(false)
    expect(isReportTab(undefined)).toBe(false)
    expect(REPORT_TABS).toHaveLength(7)
  })

  it('parseMonths aceita 6/12/24/36 e cai no padrão para o resto', () => {
    expect(parseMonths('6')).toBe(6)
    expect(parseMonths('36')).toBe(36)
    expect(parseMonths('7')).toBe(DEFAULT_MONTHS)
    expect(parseMonths(undefined)).toBe(DEFAULT_MONTHS)
    expect(parseMonths('abc')).toBe(DEFAULT_MONTHS)
  })
})

describe('charts helpers', () => {
  it('formata chaves de série diárias e mensais', () => {
    expect(formatSeriesKey('2026-11-05')).toBe('05/11')
    expect(formatSeriesKeyLong('2026-11-05')).toBe('05/11/2026')
    expect(formatSeriesKey('2026-11')).toMatch(/nov/i)
    expect(formatSeriesKeyLong('2026-11')).toMatch(/novembro/i)
  })

  it('colorFor usa a cor definida ou uma reserva cíclica', () => {
    expect(colorFor(0, '#123456')).toBe('#123456')
    expect(colorFor(0, null)).toBe(colorFor(9, null))
  })
})
