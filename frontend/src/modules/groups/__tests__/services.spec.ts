import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { groupDashboardService } from '../services/groupDashboardService'
import { groupService } from '../services/groupService'
import { groupTransactionService, toGroupTransactionParams } from '../services/groupTransactionService'
import { invitationService } from '../services/invitationService'

describe('serviços de grupos', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  describe('groupService', () => {
    it('lista sem `includeArchived` por padrão e com `includeArchived=true` quando pedido', async () => {
      mock.onGet('/groups').reply(200, [])
      await groupService.list()
      await groupService.list(true)
      expect(mock.history.get[0]?.params).toEqual({})
      expect(mock.history.get[1]?.params).toEqual({ includeArchived: true })
    })

    it('usa os verbos e rotas do contrato para grupo e membros', async () => {
      mock.onPost('/groups').reply(201, { id: 'g1' })
      mock.onGet('/groups/g1').reply(200, { id: 'g1' })
      mock.onPut('/groups/g1').reply(200, { id: 'g1' })
      mock.onPost('/groups/g1/archive').reply(200, { id: 'g1' })
      mock.onPost('/groups/g1/unarchive').reply(200, { id: 'g1' })
      mock.onPost('/groups/g1/leave').reply(204)
      mock.onGet('/groups/g1/members').reply(200, [])
      mock.onPut('/groups/g1/members/m1/role').reply(200, { id: 'm1' })
      mock.onDelete('/groups/g1/members/m1').reply(204)

      await groupService.create({ name: 'Casa', description: null })
      await groupService.get('g1')
      await groupService.update('g1', { name: 'Casa 2' })
      await groupService.archive('g1')
      await groupService.unarchive('g1')
      await groupService.leave('g1')
      await groupService.listMembers('g1', true)
      await groupService.changeRole('g1', 'm1', 'Admin')
      await groupService.removeMember('g1', 'm1')

      expect(JSON.parse(mock.history.post[0]?.data)).toEqual({ name: 'Casa', description: null })
      expect(mock.history.get[1]?.params).toEqual({ includeFormer: true })
      expect(JSON.parse(mock.history.put[1]?.data)).toEqual({ role: 'Admin' })
      expect(mock.history.delete).toHaveLength(1)
    })

    it('convites: lista com status, cria, reenvia e revoga', async () => {
      mock.onGet('/groups/g1/invitations').reply(200, [])
      mock.onPost('/groups/g1/invitations').reply(201, { id: 'i1', emailSent: true })
      mock.onPost('/groups/g1/invitations/i1/resend').reply(200, { id: 'i1' })
      mock.onDelete('/groups/g1/invitations/i1').reply(204)

      await groupService.listInvitations('g1', 'Pending')
      const created = await groupService.invite('g1', 'a@b.com')
      await groupService.resendInvitation('g1', 'i1')
      await groupService.revokeInvitation('g1', 'i1')

      expect(mock.history.get[0]?.params).toEqual({ status: 'Pending' })
      expect(JSON.parse(mock.history.post[0]?.data)).toEqual({ email: 'a@b.com' })
      expect(created.emailSent).toBe(true)
    })
  })

  describe('invitationService', () => {
    it('codifica o token na URL e usa preview/accept/decline', async () => {
      mock.onGet('/invitations/a%2Fb').reply(200, { groupName: 'Casa' })
      mock.onPost('/invitations/a%2Fb/accept').reply(200, { id: 'g1' })
      mock.onPost('/invitations/a%2Fb/decline').reply(204)

      expect((await invitationService.preview('a/b')).groupName).toBe('Casa')
      expect((await invitationService.accept('a/b')).id).toBe('g1')
      await invitationService.decline('a/b')
      expect(mock.history.post).toHaveLength(2)
    })
  })

  describe('groupTransactionService', () => {
    it('toGroupTransactionParams remove vazios e faz trim da busca', () => {
      expect(
        toGroupTransactionParams({
          page: 2,
          pageSize: 20,
          search: '  luz ',
          createdByUserId: 'u1',
          paidByUserId: undefined,
          categoryId: '',
        }),
      ).toEqual({ page: 2, pageSize: 20, search: 'luz', createdByUserId: 'u1' })
    })

    it('envia os filtros do contrato como query string', async () => {
      mock
        .onGet('/groups/g1/transactions')
        .reply(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
      await groupTransactionService.list('g1', {
        from: '2026-11-01',
        to: '2026-11-30',
        status: 'Overdue',
        createdByUserId: 'u1',
        paidByUserId: 'u2',
      })
      expect(mock.history.get[0]?.params).toEqual({
        from: '2026-11-01',
        to: '2026-11-30',
        status: 'Overdue',
        createdByUserId: 'u1',
        paidByUserId: 'u2',
      })
    })

    it('usa as rotas de detalhe, ações, histórico e pagamentos do grupo', async () => {
      const base = '/groups/g1/transactions/t1'
      mock.onGet(base).reply(200, {})
      mock.onPost('/groups/g1/transactions').reply(201, {})
      mock.onPut(base).reply(200, {})
      mock.onDelete(base).reply(204)
      mock.onPost(`${base}/cancel`).reply(200, {})
      mock.onPost(`${base}/reopen`).reply(200, {})
      mock.onGet(`${base}/history`).reply(200, [])
      mock.onPost(`${base}/payments`).reply(201, {})
      mock.onDelete(`${base}/payments/p1`).reply(200, {})
      mock.onPut(`${base}/payments/p1`).reply(200, {})

      await groupTransactionService.get('g1', 't1')
      await groupTransactionService.create('g1', {
        description: 'x',
        amount: 1,
        categoryId: 'c',
        transactionDate: '2026-11-01',
      })
      await groupTransactionService.update('g1', 't1', {
        description: 'x',
        amount: 1,
        categoryId: 'c',
        transactionDate: '2026-11-01',
      })
      await groupTransactionService.remove('g1', 't1')
      await groupTransactionService.cancel('g1', 't1')
      await groupTransactionService.reopen('g1', 't1')
      await groupTransactionService.history('g1', 't1')
      await groupTransactionService.addPayment('g1', 't1', { amount: 10, paymentDate: '2026-11-10', accountId: 'a1' })
      await groupTransactionService.updatePayment('g1', 't1', 'p1', {
        amount: 10,
        interestAmount: 2.5,
        paymentDate: '2026-11-10',
      })
      await groupTransactionService.removePayment('g1', 't1', 'p1')

      const payment = mock.history.post.find((r) => r.url === `${base}/payments`)
      expect(JSON.parse(payment?.data)).toEqual({ amount: 10, paymentDate: '2026-11-10', accountId: 'a1' })
      expect(JSON.parse(mock.history.put.find((r) => r.url === `${base}/payments/p1`)?.data)).toEqual({
        amount: 10,
        interestAmount: 2.5,
        paymentDate: '2026-11-10',
      })
      expect(mock.history.delete).toHaveLength(2)
    })
  })

  describe('groupDashboardService', () => {
    it('envia from/to e só envia months/endMonth quando informados', async () => {
      mock.onGet('/groups/g1/dashboard').reply(200, {})
      await groupDashboardService.get('g1', { from: '2026-11-01', to: '2026-11-30' })
      await groupDashboardService.get('g1', { from: '2026-11-01', to: '2026-11-30', months: 12, endMonth: '2026-11' })
      expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30' })
      expect(mock.history.get[1]?.params).toEqual({
        from: '2026-11-01',
        to: '2026-11-30',
        months: 12,
        endMonth: '2026-11',
      })
    })
  })
})
