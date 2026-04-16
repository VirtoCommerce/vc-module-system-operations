export interface ModuleInfo {
  id: string;
  version: string;
  owners: string[];
}

export interface SystemInfo {
  platformVersion: string;
  installedModules: ModuleInfo[];
  [key: string]: unknown;
}

export interface SampleDataPackage {
  name: string;
  description: string | null;
  size: string | number | null;
  url: string | null;
  platformVersion: string | null;
}

export interface ExportManifestModule {
  id: string;
  version: string;
  isChecked: boolean;
}

export interface ExportManifest {
  author: string;
  platformVersion: string;
  created: string;
  handleSecurity: boolean;
  handleSettings: boolean;
  handleDynamicProperties: boolean;
  handleBinaryData: boolean;
  modules: ExportManifestModule[];
}

export interface ExportImportRequest {
  fileUrl?: string;
  handleSecurity: boolean;
  handleSettings: boolean;
  handleDynamicProperties: boolean;
  handleBinaryData: boolean;
  modules: string[];
}

export interface ExportImportNotification {
  id: string;
  jobId: string;
  title: string;
  description: string;
  created: string;
  finished: string | null;
  totalCount: number;
  processedCount: number;
  errorCount: number;
  errors: string[];
  downloadUrl?: string;
}

export interface UploadedAsset {
  url: string;
  name: string;
  size: number;
}

export type SampleDataState = 'Undefined' | 'Processing' | 'Completed';

export interface ObjectSettingEntry {
  name: string;
  value: unknown;
  valueType?: string;
  values?: unknown[];
  defaultValue?: unknown;
  [key: string]: unknown;
}

export interface PushNotification {
  id: string;
  notifyType?: string;
  title?: string;
  description?: string;
  created?: string;
  finished?: string | null;
  isNew?: boolean;
  [key: string]: unknown;
}

export interface PushNotificationSearchResult {
  totalCount: number;
  newCount: number;
  notifyEvents: PushNotification[];
}

export type DialogType = 'warning' | 'success' | 'error';

export interface DialogState {
  visible: boolean;
  type: DialogType;
  title: string;
  message: string;
  showCancel: boolean;
  resolve: ((value: boolean) => void) | null;
}
