import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { TransactionStatus } from '@/types/api'
import TransactionStatusChip from '../components/TransactionStatusChip.vue'

const EXPECTED: Record<TransactionStatus, string> = {
  Planned: 'Previsto',
  Pending: 'Pendente',
  Paid: 'Pago',
  Received: 'Recebido',
  Overdue: 'Atrasado',
  Canceled: 'Cancelado',
}

describe('TransactionStatusChip', () => {
  it.each(Object.entries(EXPECTED))('exibe texto e ícone para %s (não depende só da cor)', (status, label) => {
    const wrapper = mount(TransactionStatusChip, { props: { status: status as TransactionStatus } })
    expect(wrapper.text()).toBe(label)
    const icon = wrapper.find('i')
    expect(icon.exists()).toBe(true)
    expect(icon.attributes('aria-hidden')).toBe('true')
  })

  it('aplica uma classe de tom por situação', () => {
    expect(mount(TransactionStatusChip, { props: { status: 'Overdue' } }).classes()).toContain('status-chip--danger')
    expect(mount(TransactionStatusChip, { props: { status: 'Paid' } }).classes()).toContain('status-chip--success')
  })
})
