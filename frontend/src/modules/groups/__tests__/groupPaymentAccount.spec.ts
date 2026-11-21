import { describe, expect, it } from 'vitest'
import { buildGroupPaymentRequest } from '../validation'

const base = { amount: '500,00', interest: '23,50', paymentDate: '2026-11-17', paymentMethod: null }

describe('buildGroupPaymentRequest — conta privada na edição', () => {
  it('não envia clearAccount nem accountId quando o vínculo atual deve ser mantido', () => {
    const request = buildGroupPaymentRequest({ ...base, accountId: null, clearAccount: false })
    expect(request.accountId).toBeNull()
    expect('clearAccount' in request).toBe(false)
  })

  it('envia clearAccount=true para desvincular', () => {
    expect(buildGroupPaymentRequest({ ...base, accountId: null, clearAccount: true }).clearAccount).toBe(true)
  })

  it('escolher outra conta prevalece sobre desvincular', () => {
    const request = buildGroupPaymentRequest({ ...base, accountId: 'acc-1', clearAccount: true })
    expect(request.accountId).toBe('acc-1')
    expect('clearAccount' in request).toBe(false)
  })
})
