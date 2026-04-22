<script setup lang="ts">
defineProps<{
  icon: string;
  iconColor: 'blue' | 'red' | 'orange' | 'green' | 'purple';
  title: string;
  permission?: string;
  description?: string;
  scenario?: string;
  variant?: 'danger' | 'warning';
}>();
</script>

<template>
  <div class="op-card" :class="variant ? `op-card--${variant}` : ''">
    <div class="op-card__header">
      <div class="op-card__icon" :class="`op-card__icon--${iconColor}`">
        <i :class="icon"></i>
      </div>
      <div class="op-card__title-area">
        <div class="op-card__title">{{ title }}</div>
        <div v-if="permission" class="op-card__permission">{{ permission }}</div>
      </div>
    </div>
    <div v-if="description" class="op-card__description">{{ description }}</div>
    <div v-if="scenario" class="op-card__scenario">
      <i class="fas fa-lightbulb op-card__scenario-icon"></i>
      <span class="op-card__scenario-text">{{ scenario }}</span>
    </div>
    <slot></slot>
  </div>
</template>
