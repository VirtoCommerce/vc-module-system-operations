<script setup lang="ts">
defineProps<{
  title: string;
}>();
</script>

<template>
  <div class="section">
    <div class="section-title">{{ title }}</div>
    <div class="cards-grid">
      <slot></slot>
    </div>
  </div>
</template>
