// An image that needs the sign-in token (job photos). Phones send headers with images; browsers can't,
// so on the web the file is downloaded with the token and shown from memory.
import React, { useEffect, useState } from 'react';
import { Image, Linking, Platform, Pressable } from 'react-native';
import { authHeaders, authorizedObjectUrl } from '../../utils/api';

export function useAuthorizedUri(url) {
  const [uri, setUri] = useState(Platform.OS === 'web' ? null : url);
  useEffect(() => {
    if (Platform.OS !== 'web' || !url) return undefined;
    let objectUrl = null;
    let cancelled = false;
    authorizedObjectUrl(url).then((u) => {
      if (cancelled) URL.revokeObjectURL(u);
      else { objectUrl = u; setUri(u); }
    }).catch(() => {});
    return () => { cancelled = true; if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [url]);
  return uri;
}

/** Tapping opens the full photo (web: in a new tab). */
export default function AuthImage({ url, style, resizeMode, accessibilityLabel }) {
  const uri = useAuthorizedUri(url);
  return (
    <Pressable onPress={() => uri && Platform.OS === 'web' && Linking.openURL(uri)} accessibilityRole="imagebutton" accessibilityLabel={accessibilityLabel}>
      {uri ? (
        <Image source={Platform.OS === 'web' ? { uri } : { uri, headers: authHeaders() }} style={style} resizeMode={resizeMode} />
      ) : (
        <Image source={{ uri: undefined }} style={style} />
      )}
    </Pressable>
  );
}
