using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class TextureCache {
	private static Dictionary <Texture, RaytracerTexture2d> cache = new Dictionary <Texture, RaytracerTexture2d> ();

	public static RaytracerTexture2d FromUnityTexture ( Texture2D unityTexture ) {
		RaytracerTexture2d texture;

		if ( !cache.TryGetValue ( unityTexture, out texture ) ) {
			texture = RaytracerTexture2d.FromUnityTexture ( unityTexture );
			cache [unityTexture] = texture;
		}

		return	texture;
	}
}
