import type { PressableStateCallbackType } from 'react-native';

/** Pressed, or hovered with a mouse on the web (react-native-web adds `hovered`). */
export const isActive = (state: PressableStateCallbackType) =>
  state.pressed || (state as { hovered?: boolean }).hovered === true;
