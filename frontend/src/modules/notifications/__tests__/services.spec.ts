import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { notificationService } from '../services/notificationService'
import { makePage, makeNotification } from './fixtures'

describe('notificationService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista com paginação e envia `unreadOnly` somente quando verdadeiro', async () => {
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))

    const result = await notificationService.list({ page: 2, pageSize: 10, unreadOnly: true })
    await notificationService.list()

    expect(result.items).toHaveLength(1)
    expect(mock.history.get[0]?.params).toEqual({ page: 2, pageSize: 10, unreadOnly: true })
    expect(mock.history.get[1]?.params).toEqual({})
  })

  it('lê o contador de não lidas', async () => {
    mock.onGet('/notifications/unread-count').reply(200, { count: 7 })
    expect(await notificationService.unreadCount()).toBe(7)
  })

  it('usa as rotas e verbos do contrato para ler, ler tudo, aceitar e recusar', async () => {
    mock.onPost('/notifications/n1/read').reply(204)
    mock.onPost('/notifications/read-all').reply(204)
    mock.onPost('/notifications/n1/accept-invitation').reply(200, { id: 'g9', name: 'Casa' })
    mock.onPost('/notifications/n1/decline-invitation').reply(204)

    await notificationService.markRead('n1')
    await notificationService.markAllRead()
    const group = await notificationService.acceptInvitation('n1')
    await notificationService.declineInvitation('n1')

    expect(group).toMatchObject({ id: 'g9' })
    expect(mock.history.post.map((r) => r.url)).toEqual([
      '/notifications/n1/read',
      '/notifications/read-all',
      '/notifications/n1/accept-invitation',
      '/notifications/n1/decline-invitation',
    ])
  })
})
