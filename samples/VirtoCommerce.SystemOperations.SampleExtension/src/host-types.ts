// Inlined copy of the host's plugin contract. Kept as a tiny standalone file
// so the sample doesn't need a runtime npm dependency on the host package.
// Whenever `vc-module-system-operations` updates `app/plugins/types.ts`,
// mirror the relevant changes here.

import type { Component } from 'vue';

export type SystemOperationsSection =
  | 'maintenance'
  | 'data'
  | 'diagnostics'
  | 'plugins';

export type OperationCardIconColor = 'blue' | 'red' | 'orange' | 'green' | 'purple';
export type OperationCardVariant = 'danger' | 'warning';

export interface OperationCardProps {
  icon: string;
  iconColor: OperationCardIconColor;
  title: string;
  description?: string;
  scenario?: string;
  permission?: string;
  variant?: OperationCardVariant;
}

export interface RegisterCardOptions {
  section?: SystemOperationsSection;
  component: Component;
  props: OperationCardProps;
  order?: number;
}

export interface SystemOperationsHost {
  registerCard(card: RegisterCardOptions): void;
}

export interface PluginContext {
  pluginId: string;
  pluginVersion: string;
  isDev: boolean;
}

export interface SystemOperationsPlugin {
  install(host: SystemOperationsHost, ctx: PluginContext): void | Promise<void>;
}
