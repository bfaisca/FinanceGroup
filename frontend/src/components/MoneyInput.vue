<script setup lang="ts">
import InputGroup from 'primevue/inputgroup'
import InputGroupAddon from 'primevue/inputgroupaddon'
import InputText from 'primevue/inputtext'
import { normalizeMoneyText } from '@/utils/money'

/**
 * Campo monetário em BRL. O modelo é o TEXTO digitado (pt-BR); a conversão para centavos
 * (`parseMoneyToCents`) é feita pelo formulário, evitando float. Reformata ao sair do campo.
 */
const props = withDefaults(
  defineProps<{
    id: string
    describedBy?: string
    invalid?: boolean
    allowNegative?: boolean
    disabled?: boolean
    autofocus?: boolean
  }>(),
  { describedBy: undefined, invalid: false, allowNegative: false, disabled: false, autofocus: false },
)

const model = defineModel<string>({ required: true })

function onBlur(): void {
  model.value = normalizeMoneyText(model.value, { allowNegative: props.allowNegative })
}
</script>

<template>
  <InputGroup>
    <InputGroupAddon aria-hidden="true">R$</InputGroupAddon>
    <InputText
      :id="id"
      v-model="model"
      inputmode="decimal"
      autocomplete="off"
      placeholder="0,00"
      :aria-describedby="describedBy"
      :aria-invalid="invalid"
      :invalid="invalid"
      :disabled="disabled"
      :autofocus="autofocus"
      @blur="onBlur"
    />
  </InputGroup>
</template>
