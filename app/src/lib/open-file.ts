// Phones: download a signed-in file (e.g. a proof pack) to the cache, then show it in the system viewer, which also
// offers sharing it (WhatsApp, email, print).
import { Directory, File, Paths } from 'expo-file-system';
import { API_URL, authHeader } from '@/api/client';

export async function openAuthorizedFile(path: string, fileName: string, mimeType = 'application/pdf'): Promise<void> {
  const folder = new Directory(Paths.cache, 'downloads');
  if (!folder.exists) folder.create({ intermediates: true });
  const target = new File(folder, fileName);
  if (target.exists) target.delete();
  await File.downloadFileAsync(`${API_URL}${path}`, target, { headers: authHeader() });
  await File.preview(target, { title: fileName, mimeType });
}
