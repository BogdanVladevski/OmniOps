// Web: fetch a signed-in file (browsers can't send the token on a plain link) and open it in a new tab.
import { fileObjectUrl } from '@/api/client';

export async function openAuthorizedFile(path: string, fileName: string, _mimeType = 'application/pdf'): Promise<void> {
  const url = await fileObjectUrl(path);
  const opened = window.open(url, '_blank'); // a same-origin blob URL; window.open returns null only when blocked
  if (!opened) {
    // Pop-up blocked: download instead.
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
