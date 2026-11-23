import { http } from '@/services/http'

/** Vínculo com o bot do Telegram — docs/API_CONTRACTS.md §18. */
export interface TelegramLinkStatus {
  configured: boolean
  linked: boolean
  linkedAt: string | null
  botUsername: string | null
}

export interface TelegramLinkCode {
  code: string
  expiresAt: string
  botUsername: string
  deepLink: string
}

export const telegramService = {
  async status(): Promise<TelegramLinkStatus> {
    const { data } = await http.get<TelegramLinkStatus>('/telegram/link')
    return data
  },

  async createCode(): Promise<TelegramLinkCode> {
    const { data } = await http.post<TelegramLinkCode>('/telegram/link-code')
    return data
  },

  async unlink(): Promise<void> {
    await http.delete('/telegram/link')
  },
}
