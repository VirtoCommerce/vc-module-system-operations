// Public types for System Operations plugin authors.
//
// A plugin module is an MF remote whose default export implements
// `SystemOperationsPlugin`. The host calls `install(host, ctx)` once after
// loading the remote; the plugin uses `host.registerCard(...)` (and any
// future host APIs) to contribute UI.
//
// Plugins should depend on this file via a relative path during development
// or copy/inline the types — the contract is intentionally tiny so a plugin
// doesn't need to add a runtime dependency on the host package.

import type { Component } from 'vue';

/** Sections of the System Operations page a plugin card can attach to. */
export type SystemOperationsSection =
  | 'maintenance'
  | 'data'
  | 'diagnostics'
  | 'plugins';

/** Visual treatment for the OperationCard wrapper. */
export type OperationCardIconColor = 'blue' | 'red' | 'orange' | 'green' | 'purple';
export type OperationCardVariant = 'danger' | 'warning';

/**
 * Props that match the host's <OperationCard> component shape. The host
 * renders one OperationCard per registered card and slots
 * <component :is="card.component" /> as the card body.
 */
export interface OperationCardProps {
  icon: string;                          // Font Awesome class, e.g. "fas fa-bolt"
  iconColor: OperationCardIconColor;
  title: string;
  description?: string;
  scenario?: string;
  permission?: string;
  variant?: OperationCardVariant;
}

/** Registration payload accepted by `host.registerCard`. */
export interface RegisterCardOptions {
  /** Which section to render the card in. Defaults to "plugins". */
  section?: SystemOperationsSection;
  /** Card body — rendered inside the OperationCard slot. */
  component: Component;
  /** Visual props forwarded to the OperationCard. */
  props: OperationCardProps;
  /** Sort order within the section (ascending). Defaults to 0. */
  order?: number;
}

/**
 * Host API surface visible to a plugin. The host scopes one of these per
 * plugin so it can attribute registrations back to the owning plugin id.
 */
export interface SystemOperationsHost {
  /** Add a card to a known section. Plugin can call this any number of times. */
  registerCard(card: RegisterCardOptions): void;
}

/** Per-plugin context passed to `install`. */
export interface PluginContext {
  pluginId: string;
  pluginVersion: string;
  /** True when the host is currently running in a preview/dev environment. */
  isDev: boolean;
}

/** Default export shape for a plugin's MF entry. */
export interface SystemOperationsPlugin {
  install(host: SystemOperationsHost, ctx: PluginContext): void | Promise<void>;
}
