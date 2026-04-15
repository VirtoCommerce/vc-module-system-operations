import { useApi } from './useApi';
import type { SystemInfo } from '../types';

export function useSystemInfo() {
  const { get } = useApi();

  function downloadJson(data: string, filename: string) {
    const blob = new Blob([data], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  function isVirtoModule(mod: { owners: string[] }): boolean {
    return mod.owners?.some((x) => x.toLowerCase().replace(/\s+/g, '') === 'virtocommerce') ?? false;
  }

  async function downloadManifest(): Promise<void> {
    const data = await get<SystemInfo>('/api/platform/diagnostics/systeminfo');
    downloadJson(JSON.stringify(data, null, '\t'), 'vc-platform-info.json');
  }

  async function downloadPackage(): Promise<void> {
    const data = await get<SystemInfo>('/api/platform/diagnostics/systeminfo');

    const installedModules = [...data.installedModules]
      .sort((a, b) => a.id.localeCompare(b.id))
      .sort((a, b) => {
        const aVirto = isVirtoModule(a);
        const bVirto = isVirtoModule(b);
        if (aVirto && !bVirto) return -1;
        if (!aVirto && bVirto) return 1;
        return 0;
      })
      .map((x) => ({ Id: x.id, Version: x.version }));

    const packages = {
      ModuleSources: ['https://raw.githubusercontent.com/VirtoCommerce/vc-modules/master/modules_v3.json'],
      PlatformVersion: data.platformVersion,
      Modules: installedModules,
    };

    downloadJson(JSON.stringify(packages, null, '\t'), 'vc-package.json');
  }

  return { downloadManifest, downloadPackage };
}
