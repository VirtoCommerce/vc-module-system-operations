// Plugin entry. The MF remote exposes this as `./Module`; the host loads it
// via `mf.loadRemote('VirtoCommerce.SystemOperations.SampleExtension/Module')`
// and calls `install(host, ctx)`.

import BrowserInfoCard from './BrowserInfoCard.vue';
import type { SystemOperationsPlugin } from './host-types';

const plugin: SystemOperationsPlugin = {
  install(host, ctx) {
    // Register a "Browser Info" card. Lands in the Diagnostics section
    // alongside the platform's built-in diagnostic widgets.
    host.registerCard({
      section: 'diagnostics',
      component: BrowserInfoCard,
      props: {
        icon: 'fas fa-globe',
        iconColor: 'blue',
        title: 'Browser Info',
        description:
          'Shows the running browser, OS, screen size, and time zone — handy when triaging client-side bug reports.',
        scenario: `Contributed by ${ctx.pluginId}@${ctx.pluginVersion}.`,
      },
    });
  },
};

export default plugin;
