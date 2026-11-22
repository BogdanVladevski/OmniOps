// Share a link: the phone's share sheet (WhatsApp, email…) on mobile, the clipboard on the web.
import { Platform, Share } from 'react-native';

/** Returns a short notice for the screen (or null when the share sheet handled it). */
export async function shareLink(url, message) {
  if (Platform.OS !== 'web') {
    await Share.share({ message: `${message} ${url}` });
    return null;
  }
  try {
    // Unavailable on plain-http sites other than localhost, or when the browser blocks it.
    await navigator.clipboard.writeText(url);
    return 'Link copied';
  } catch {
    return `Copy this link: ${url}`;
  }
}
