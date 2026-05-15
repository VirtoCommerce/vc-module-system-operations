import { createApp } from 'vue';
import App from './App.vue';
import './styles/variables.css';
import './styles/dialog.css';
import './styles/main.css';
import { createPluginRegistry, PluginRegistryKey } from './plugins/registry';
import { loadPlugins } from './plugins/loader';

const registry = createPluginRegistry();

const app = createApp(App);
app.provide(PluginRegistryKey, registry);
app.mount('#app');

// Discover and load plugins asynchronously. The shell renders immediately;
// plugin cards appear when their remotes finish loading. Errors are isolated
// inside loader.ts so they never block page bootstrap.
const isDev = import.meta.env.DEV;
loadPlugins(registry, isDev);
