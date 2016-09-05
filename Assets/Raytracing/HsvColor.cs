using UnityEngine;
using System.Collections;

public struct HsvColor {
	public float h;
	public float s;
	public float v;
	public float a;

	public HsvColor ( float hue, float saturation, float value, float alpha = 1 ) {
		this.h = hue;
		this.s = saturation;
		this.v = value;
		this.a = alpha;
	}

	public static Color ChangeHue ( Color c, float hueDelta ) {
		HsvColor hsv = ( HsvColor ) c;
		hsv.h = ( hsv.h + hueDelta ) % 1;
		c = ( Color ) hsv;

		return	c;
	}

	#region Rgba to Hsv
	public static HsvColor FromRgba ( float r, float g, float b, float a ) {
		float min = Mathf.Min ( r, g, b );
		float max = Mathf.Max ( r, g, b );
		float dm = max - min;
		float h, s, v = max;

		if ( dm == 0 ) {
			h = 0;
			s = 0;
		} else {
			s = dm / max;

			float dr = ( ( ( max - r ) / 6 ) + ( dm / 2 ) ) / dm;
			float dg = ( ( ( max - g ) / 6 ) + ( dm / 2 ) ) / dm;
			float db = ( ( ( max - b ) / 6 ) + ( dm / 2 ) ) / dm;

			if ( r == max )
				h = db - dg;
			else if ( g == max )
				h = ( 1.0f / 3 ) + dr - db;
			else /*if ( b == max )*/
				h = ( 2.0f / 3 ) + dg - dr;

			if ( h < 0 ) h += 1;
			if ( h > 1 ) h -= 1;
		}

		return	new HsvColor ( h, s, v, a );
	}

	public static HsvColor FromRgba ( Color c ) {
		return	FromRgba ( c.r, c.g, c.b, c.a );
	}

	public static explicit operator HsvColor ( Color c ) {
		return	FromRgba ( c );
	}
	#endregion Rgba to Hsv

	#region Hsv to Rgba
	public static Color ToRgba ( float h, float s, float v, float a ) {
		float r, g, b;

		if ( s == 0 )
			r = g = b = v;
        else {
			h = h * 6;
			float i = Mathf.Floor ( h );
			float x = v * ( 1 - s );
			float y = v * ( 1 - ( s * ( h - i ) ) );
			float z = v * ( 1 - ( s * ( 1 - ( h - i ) ) ) );

			if ( i == 0 ) {
				r = v;
				g = z;
				b = x;
			} else if ( i == 1 ) {
				r = y;
				g = v;
				b = x;
			} else if ( i == 2 ) {
				r = x;
				g = v;
				b = z;
			} else if ( i == 3 ) {
				r = x;
				g = y;
				b = v;
			} else if ( i == 4 ) {
				r = z;
				g = x;
				b = v;
			} else {
				r = v;
				g = x;
				b = y;
			}
        }

		return	new Color ( r, g, b, a );
	}

	public static Color ToRgba ( HsvColor c ) {
		return	ToRgba ( c.h, c.s, c.v, c.a );
	}

	public static explicit operator Color ( HsvColor c ) {
		return	ToRgba ( c );
	}
	#endregion Hsv to Rgba

	public override bool Equals ( object obj ) {
		if ( object.ReferenceEquals ( obj, null ) || !( obj is HsvColor ) )
			return	false;

		var other = ( HsvColor ) obj;

		return	this.h == other.h && this.s == other.s && this.v == other.v && this.a == other.a;
	}

	public override int GetHashCode () {
		return	h.GetHashCode () ^ s.GetHashCode () ^ v.GetHashCode () ^ a.GetHashCode ();
	}

	public override string ToString () {
		return	string.Format (
			"h: {0}, s: {1}, v: {2}, a: {3}",
			h, s, v, a
		);
	}
}