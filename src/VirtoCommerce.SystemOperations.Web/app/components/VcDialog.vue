<script setup lang="ts">
import { inject } from 'vue';
import { DialogKey } from '../composables/useDialog';
import { I18nKey } from '../composables/useI18n';

const dialog = inject(DialogKey)!;
const { t } = inject(I18nKey)!;

const iconMap: Record<string, string> = {
  warning: '/images/dialogs/icon-warning.svg',
  success: '/images/dialogs/icon-success.svg',
  error: '/images/dialogs/icon-error.svg',
};
</script>

<template>
  <Teleport to="body">
    <Transition name="dialog">
      <div v-if="dialog.state.visible" class="vc-modal-overlay" @click.self="dialog.close(false)">
        <div class="vc-modal-content">
          <div class="vc-modal-header" :class="`vc-modal-header--${dialog.state.type}`">
            <span class="vc-modal-title">{{ dialog.state.title }}</span>
            <button class="vc-modal-close" @click="dialog.close(false)">
              <i class="fa fa-times"></i>
            </button>
          </div>
          <div class="vc-modal-body">
            <img
              v-if="iconMap[dialog.state.type]"
              class="vc-modal-icon"
              :src="iconMap[dialog.state.type]"
              alt=""
              width="30"
              height="30"
            >
            <div class="vc-modal-message">{{ dialog.state.message }}</div>
          </div>
          <div class="vc-modal-footer">
            <button
              v-if="dialog.state.showCancel"
              class="vc-modal-btn vc-modal-btn--outline"
              @click="dialog.close(true)"
            >
              {{ t('dialog.yes') }}
            </button>
            <button
              class="vc-modal-btn vc-modal-btn--primary"
              @click="dialog.close(dialog.state.showCancel ? false : true)"
            >
              {{ dialog.state.showCancel ? t('dialog.no') : t('dialog.ok') }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
