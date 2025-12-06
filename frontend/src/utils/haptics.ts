/**
 * Haptic feedback utilities for mobile devices
 * Provides tactile feedback for user interactions
 */

export const haptics = {
  /**
   * Light tap feedback - for button presses, selections
   */
  light: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate(10);
    }
  },

  /**
   * Medium impact - for confirmations, toggles
   */
  medium: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate(20);
    }
  },

  /**
   * Heavy impact - for important actions, errors
   */
  heavy: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate(50);
    }
  },

  /**
   * Success pattern - for successful submissions
   */
  success: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate([10, 50, 10]);
    }
  },

  /**
   * Error pattern - for errors, validation failures
   */
  error: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate([50, 100, 50, 100, 50]);
    }
  },

  /**
   * Warning pattern - for warnings, deadlines
   */
  warning: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate([30, 50, 30]);
    }
  },

  /**
   * Notification pattern - for new updates, results
   */
  notification: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate([20, 50, 20, 50, 20]);
    }
  },

  /**
   * Selection pattern - for picking teams, making choices
   */
  selection: () => {
    if ('vibrate' in navigator) {
      navigator.vibrate(15);
    }
  },
};

/**
 * Check if haptic feedback is supported
 */
export const isHapticsSupported = (): boolean => {
  return 'vibrate' in navigator;
};

/**
 * Request notification permission (for push notifications)
 */
export const requestNotificationPermission = async (): Promise<NotificationPermission> => {
  if ('Notification' in window) {
    return await Notification.requestPermission();
  }
  return 'denied';
};
