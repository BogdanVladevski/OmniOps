// An image that needs the sign-in (job photos). Phones send the header with the image request; browsers can't,
// so on the web the file is downloaded with the token and shown from memory.
import { useEffect, useState } from 'react';
import { Image, Linking, Platform, Pressable, type ImageStyle, type StyleProp } from 'react-native';
import { API_URL, authHeader, fileObjectUrl } from '@/api/client';

export function useAuthorizedUri(path: string | null | undefined) {
  const [uri, setUri] = useState<string | null>(Platform.OS === 'web' || !path ? null : `${API_URL}${path}`);
  useEffect(() => {
    if (Platform.OS !== 'web' || !path) return;
    let objectUrl: string | null = null;
    let cancelled = false;
    fileObjectUrl(path)
      .then((u) => {
        if (cancelled) URL.revokeObjectURL(u);
        else setUri((objectUrl = u));
      })
      .catch(() => {});
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [path]);
  return uri;
}

/** Tapping opens the full photo (web: in a new tab). */
export function AuthImage({
  path,
  style,
  label,
  fit = 'cover',
}: {
  path: string;
  style: StyleProp<ImageStyle>;
  label: string;
  fit?: 'cover' | 'contain';
}) {
  const uri = useAuthorizedUri(path);
  return (
    <Pressable
      onPress={() => uri && Platform.OS === 'web' && Linking.openURL(uri)}
      accessibilityRole="imagebutton"
      accessibilityLabel={label}
    >
      <Image
        source={uri ? (Platform.OS === 'web' ? { uri } : { uri, headers: authHeader() }) : {}}
        style={style}
        resizeMode={fit}
      />
    </Pressable>
  );
}
