import { reactive, type InjectionKey } from 'vue';
import type { DialogState, DialogType } from '../types';

export const DialogKey: InjectionKey<ReturnType<typeof useDialog>> = Symbol('dialog');

export function useDialog() {
  const state = reactive<DialogState>({
    visible: false,
    type: 'warning',
    title: '',
    message: '',
    showCancel: true,
    resolve: null,
  });

  function show(type: DialogType, title: string, message: string, showCancel: boolean): Promise<boolean> {
    return new Promise((resolve) => {
      state.type = type;
      state.title = title;
      state.message = message;
      state.showCancel = showCancel;
      state.resolve = resolve;
      state.visible = true;
    });
  }

  function close(result: boolean) {
    state.visible = false;
    state.resolve?.(result);
    state.resolve = null;
  }

  function warning(title: string, message: string): Promise<boolean> {
    return show('warning', title, message, true);
  }

  function success(title: string, message: string): Promise<boolean> {
    return show('success', title, message, false);
  }

  function error(title: string, message: string): Promise<boolean> {
    return show('error', title, message, false);
  }

  return { state, close, warning, success, error };
}
