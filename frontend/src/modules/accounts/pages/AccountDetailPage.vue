<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Message from 'primevue/message'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import TransactionDetailDrawer from '@/modules/transactions/components/TransactionDetailDrawer.vue'
import TransactionFormDialog from '@/modules/transactions/components/TransactionFormDialog.vue'
import TransactionList from '@/modules/transactions/components/TransactionList.vue'
import { useTransactionList } from '@/modules/transactions/composables/useTransactionList'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import type { AccountDto, BalanceSnapshotDto, TransactionDetailDto, TransactionDto } from '@/types/api'
import { formatCurrency, formatDate, formatDateTime } from '@/utils/format'
import { ACCOUNT_TYPE_LABELS } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import BalanceSnapshotDialog from '../components/BalanceSnapshotDialog.vue'
import AccountFormDialog from '../components/AccountFormDialog.vue'
import { accountService } from '../services/accountService'

const RECENT_PAGE_SIZE = 10

const route = useRoute()
const accounts = useAccountsStore()
const categories = useCategoriesStore()

const accountId = computed(() => String(route.params.id))

const account = ref<AccountDto | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

const snapshots = ref<BalanceSnapshotDto[]>([])
const snapshotsLoading = ref(false)
const snapshotsError = ref<string | null>(null)

const page = ref(1)
const list = useTransactionList(() => ({
  accountId: accountId.value,
  page: page.value,
  pageSize: RECENT_PAGE_SIZE,
  sort: '-transactionDate',
}))

const snapshotDialogVisible = ref(false)
const editVisible = ref(false)

const detailVisible = ref(false)
const detailId = ref<string | null>(null)
const refreshToken = ref(0)
const txFormVisible = ref(false)
const editingTx = ref<TransactionDetailDto | null>(null)

async function loadAccount(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    account.value = await accountService.get(accountId.value)
  } catch (e) {
    account.value = null
    error.value = toAppError(e).message
  } finally {
    loading.value = false
  }
}

async function loadSnapshots(): Promise<void> {
  snapshotsLoading.value = true
  snapshotsError.value = null
  try {
    snapshots.value = await accountService.listSnapshots(accountId.value)
  } catch (e) {
    snapshotsError.value = toAppError(e).message
  } finally {
    snapshotsLoading.value = false
  }
}

function loadAll(): void {
  page.value = 1
  void loadAccount()
  void loadSnapshots()
}

onMounted(() => {
  void categories.load()
  void accounts.load()
  loadAll()
})

watch(accountId, loadAll)

async function onSnapshotSaved(): Promise<void> {
  // O novo saldo vira a referência: recarrega conta (projeção), histórico e a lista compartilhada.
  await Promise.all([loadAccount(), loadSnapshots(), accounts.load(true)])
}

async function onAccountSaved(): Promise<void> {
  await loadAccount()
}

function openTransaction(tx: TransactionDto): void {
  detailId.value = tx.id
  detailVisible.value = true
}

function openEditTransaction(tx: TransactionDetailDto): void {
  editingTx.value = tx
  txFormVisible.value = true
}

function onTransactionsChanged(): void {
  void list.reload()
  // Movimentações alteram o saldo projetado da conta.
  void loadAccount()
}

function onTransactionSaved(): void {
  onTransactionsChanged()
  refreshToken.value += 1
}

const canInformBalance = computed(() => account.value?.isActive === true)
</script>

<template>
  <div>
    <nav aria-label="Navegação estrutural" class="account-detail__crumbs">
      <RouterLink :to="{ name: 'accounts' }"><i class="pi pi-arrow-left" aria-hidden="true" /> Contas</RouterLink>
    </nav>

    <LoadingState v-if="loading && !account" label="Carregando conta…" />

    <Message v-else-if="error" severity="error" role="alert">
      <div class="account-detail__error">
        <span>{{ error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="loadAll" />
      </div>
    </Message>

    <template v-else-if="account">
      <PageHeader
        :title="account.name"
        :description="`${ACCOUNT_TYPE_LABELS[account.type]}${account.institution ? ' · ' + account.institution : ''}${account.isActive ? '' : ' · Inativa'}`"
      >
        <template #actions>
          <Button type="button" label="Editar" icon="pi pi-pencil" severity="secondary" @click="editVisible = true" />
          <Button
            type="button"
            label="Informar novo saldo"
            icon="pi pi-plus"
            :disabled="!canInformBalance"
            @click="snapshotDialogVisible = true"
          />
        </template>
      </PageHeader>

      <Message v-if="!account.isActive" severity="warn" class="account-detail__block">
        Conta inativa: não aceita novas movimentações nem novos saldos. Reative-a na lista de contas.
      </Message>

      <section class="surface-card account-detail__block" aria-labelledby="balances-title">
        <h2 id="balances-title" class="account-detail__title">Saldos</h2>
        <div v-if="account.latestSnapshot" class="account-detail__balances">
          <div>
            <p class="account-detail__label">Saldo informado</p>
            <p class="account-detail__value money">{{ formatCurrency(account.latestSnapshot.balance) }}</p>
            <p class="account-detail__caption">em {{ formatDate(account.latestSnapshot.referenceDate) }}</p>
          </div>
          <div>
            <p class="account-detail__label">Saldo projetado <span class="account-detail__badge">Projetado</span></p>
            <p class="account-detail__value money" data-testid="projected-balance">
              {{ formatCurrency(account.projectedBalance) }}
            </p>
            <p class="account-detail__caption">em {{ formatDate(account.projectedAsOf) }}</p>
          </div>
        </div>
        <p v-else class="account-detail__caption" data-testid="no-snapshot">
          <i class="pi pi-info-circle" aria-hidden="true" /> Informe um saldo para ver a projeção.
        </p>
        <p class="account-detail__caption">
          O saldo projetado é uma estimativa: último saldo informado mais receitas e menos despesas posteriores, até
          {{ formatDate(account.projectedAsOf) }}. Não é o saldo bancário.
        </p>
      </section>

      <section class="surface-card account-detail__block" aria-labelledby="snapshots-title">
        <h2 id="snapshots-title" class="account-detail__title">Histórico de saldos informados</h2>
        <LoadingState v-if="snapshotsLoading && snapshots.length === 0" label="Carregando histórico…" />
        <Message v-else-if="snapshotsError" severity="error" role="alert">
          <div class="account-detail__error">
            <span>{{ snapshotsError }}</span>
            <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="loadSnapshots" />
          </div>
        </Message>
        <EmptyState
          v-else-if="snapshots.length === 0"
          icon="pi pi-history"
          title="Nenhum saldo informado"
          description="Informe o saldo atual da conta para começar a acompanhar a projeção."
        >
          <Button
            v-if="canInformBalance"
            type="button"
            label="Informar novo saldo"
            icon="pi pi-plus"
            @click="snapshotDialogVisible = true"
          />
        </EmptyState>
        <DataTable
          v-else
          :value="snapshots"
          data-key="id"
          paginator
          :rows="10"
          :always-show-paginator="false"
          size="small"
        >
          <Column header="Data de referência">
            <template #body="{ data }: { data: BalanceSnapshotDto }">{{ formatDate(data.referenceDate) }}</template>
          </Column>
          <Column header="Saldo" header-class="account-detail__right" body-class="account-detail__right">
            <template #body="{ data }: { data: BalanceSnapshotDto }"
              ><span class="money">{{ formatCurrency(data.balance) }}</span></template
            >
          </Column>
          <Column header="Informado em">
            <template #body="{ data }: { data: BalanceSnapshotDto }">{{ formatDateTime(data.createdAt) }}</template>
          </Column>
        </DataTable>
      </section>

      <section class="surface-card account-detail__block" aria-labelledby="account-tx-title">
        <div class="account-detail__section-head">
          <h2 id="account-tx-title" class="account-detail__title">Movimentações da conta</h2>
          <RouterLink :to="{ name: 'transactions', query: { accountId: account.id } }">Ver todas</RouterLink>
        </div>
        <TransactionList
          :items="list.items.value"
          :loading="list.loading.value"
          :error="list.error.value"
          :total-count="list.totalCount.value"
          :page="page"
          :page-size="RECENT_PAGE_SIZE"
          sort="-transactionDate"
          hide-account
          simple-paging
          @open="openTransaction"
          @retry="list.reload()"
          @update:page="page = $event"
        />
      </section>

      <AccountFormDialog v-model:visible="editVisible" :account="account" @saved="onAccountSaved" />
      <BalanceSnapshotDialog
        v-model:visible="snapshotDialogVisible"
        :account-id="account.id"
        :account-name="account.name"
        @saved="onSnapshotSaved"
      />
      <TransactionDetailDrawer
        v-model:visible="detailVisible"
        :transaction-id="detailId"
        :refresh-token="refreshToken"
        @changed="onTransactionsChanged"
        @edit="openEditTransaction"
      />
      <TransactionFormDialog v-model:visible="txFormVisible" :transaction="editingTx" @saved="onTransactionSaved" />
    </template>
  </div>
</template>

<style scoped>
.account-detail__crumbs {
  margin-bottom: 1rem;
}

.account-detail__block {
  margin-bottom: 1.5rem;
}

.account-detail__title {
  font-size: 1.0625rem;
  font-weight: 600;
  margin-bottom: 0.75rem;
}

.account-detail__section-head {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
}

.account-detail__balances {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
  gap: 1rem;
  margin-bottom: 0.75rem;
}

.account-detail__label,
.account-detail__caption {
  margin: 0;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}

.account-detail__value {
  margin: 0.125rem 0;
  font-size: 1.5rem;
  font-weight: 700;
}

.account-detail__badge {
  padding: 0.0625rem 0.5rem;
  border-radius: 999px;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-size: 0.75rem;
  font-weight: 600;
}

.account-detail__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

:deep(.account-detail__right) {
  text-align: right;
}
</style>
