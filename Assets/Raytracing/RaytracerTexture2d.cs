using UnityEngine;
using System.Collections;
using System;

public class RaytracerTexture2d {
	private Color [,] pixels;
	public Color [,] Pixels { get { return	pixels; } }
	private int width, height;
	public int Width { get { return	width; } }
	public int Height { get { return	height; } }
	private float widthInv, heightInv;
	private RaytracerTexture2d [] mipLayers;
	public RaytracerTexture2d [] MipLayers { get { return	mipLayers; } }
	public int MipMapCount { get { return	mipLayers.Length; } }
	public TextureWrapMode WrapMode = TextureWrapMode.Clamp;
	public FilterMode FilterMode = FilterMode.Bilinear;

	public RaytracerTexture2d ( int width, int height ) {
		if ( width <= 0 )
			throw new ArgumentOutOfRangeException ( "width", width, "Value must be greater than zero." );

		if ( height <= 0 )
			throw new ArgumentOutOfRangeException ( "height", height, "Value must be greater than zero." );

		this.width = width;
		this.height = height;
		this.widthInv = 1f / width;
		this.heightInv = 1f / height;
		this.pixels = new Color [height, width];
	}

	public RaytracerTexture2d ( Color [,] pixels ) {
		this.width = pixels.GetLength ( 1 );
		this.height = pixels.GetLength ( 0 );
		this.widthInv = 1f / width;
		this.heightInv = 1f / height;
		this.pixels = pixels;
	}

	public static RaytracerTexture2d FromUnityMipLayer ( Texture2D unityTexture, int mipLevel ) {
		if ( mipLevel < 0 || mipLevel >= unityTexture.mipmapCount ) {
			throw new ArgumentOutOfRangeException (
				"mipLevel", mipLevel,
				string.Format (
					"Mip level for given texture must be in range from 0 to {0} inclusively",
					unityTexture.mipmapCount - 1
				)
			);
		}

		int width = Math.Max ( 1, unityTexture.width >> mipLevel );
		int height = Math.Max ( 1, unityTexture.height >> mipLevel );

		var texture = new RaytracerTexture2d ( width, height );
		Color [] pixelsFlat = unityTexture.GetPixels ( mipLevel );
		int i = 0;

		for ( int y = 0 ; y < height ; y++ ) {
			for ( int x = 0 ; x < width ; x++, i++ ) {
				texture.pixels [y, x] = pixelsFlat [i];
			}
		}

		texture.WrapMode = unityTexture.wrapMode;
		texture.FilterMode = unityTexture.filterMode;

		return	texture;
	}

	public static RaytracerTexture2d FromUnityTexture ( Texture2D unityTexture ) {
		var mipLayers = new RaytracerTexture2d [unityTexture.mipmapCount];

		for ( int mipLevel = 0 ; mipLevel < unityTexture.mipmapCount ; mipLevel++ ) {
			var mipLayer = FromUnityMipLayer ( unityTexture, mipLevel );
			mipLayers [mipLevel] = mipLayer;
		}
		
		var zeroLevelTexture = mipLayers [0];
		zeroLevelTexture.mipLayers = mipLayers;

		return	zeroLevelTexture;
	}

	public void NormalizeTexCoord ( ref float u, ref float v ) {
		if ( WrapMode == TextureWrapMode.Clamp ) {
			u = Mathf.Clamp01 ( u ) % 1;
			v = Mathf.Clamp01 ( v ) % 1;
		} else /*if ( WrapMode == TextureWrapMode.Repeat )*/ {
			u = u % 1;
			v = v % 1;

			if ( u < 0 )
				u = 1 + u;
			
			if ( v < 0 )
				v = 1 + v;
		}
	}

	public void GetPixelCoord ( float u, float v, out int x, out int y ) {
		NormalizeTexCoord ( ref u, ref v );
		x = ( int ) ( u * width );
		y = ( int ) ( v * height );
	}

	public Color GetPixelNearest ( float u, float v ) {
		int x, y;
		GetPixelCoord ( u, v, out x, out y );

		return	pixels [y, x];
	}

	public Color GetPixelNearest ( float u, float v, int mipLevel ) {
		return	mipLayers [mipLevel].GetPixelNearest ( u, v );
	}

	public Color GetPixelBilinear ( float u, float v ) {
		float ru = u + widthInv;
		float bv = v + heightInv;

		int rx, by;
		GetPixelCoord ( ru, bv, out rx, out by );
		NormalizeTexCoord ( ref u, ref v );

		float fx = u * width;
		float fy = v * height;
		int x = ( int ) fx;
		int y = ( int ) fy;
		float weightX = fx - x;
		float weightY = fy - y;

		Color ct = Color.Lerp ( pixels [y , x], pixels [y , rx], weightX );
		Color cb = Color.Lerp ( pixels [by, x], pixels [by, rx], weightX );
		Color c = Color.Lerp ( ct, cb, weightY );

		return	c;
	}

	public Color GetPixelBilinear ( float u, float v, int mipLevel ) {
		return	mipLayers [mipLevel].GetPixelBilinear ( u, v );
	}

	public Color GetPixelTrilinear ( float u, float v, float mipLevel ) {
		mipLevel = Mathf.Clamp ( mipLevel, 0, mipLayers.Length - 1 );
		int lowerMipLevel = Mathf.FloorToInt ( mipLevel );
		int higherMipLevel = Mathf.CeilToInt ( mipLevel );
		Color lowerMipColor = GetPixelBilinear ( u, v, lowerMipLevel );
		Color higherMipColor;

		if ( lowerMipLevel != higherMipLevel ) {
			higherMipColor = GetPixelBilinear ( u, v, higherMipLevel );
			float mix = mipLevel - lowerMipLevel;
			Color c = Color.Lerp ( lowerMipColor, higherMipColor, mix );
			
			return	c;
		} else
			return	lowerMipColor;
	}

	public Color GetFilteredPixel ( float u, float v, float mipLevel = 0 ) {
		if ( FilterMode == FilterMode.Point )
			return	GetPixelNearest ( u, v, ( int ) ( mipLevel + 0.5f ) );
		else if ( FilterMode == FilterMode.Bilinear )
			return	GetPixelBilinear ( u, v, ( int ) ( mipLevel + 0.5f ) );
		else /*if ( FilterMode == FilterMode.Trilinear )*/
			return	GetPixelTrilinear ( u, v, mipLevel );
	}
}
