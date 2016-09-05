using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public struct IntRect {
	public int X;
	public int Y;
	public int Width;
	public int Height;

	public int Right {
		get { return	X + Width; }
		set { Width = value - X; }
	}
	public int Bottom {
		get { return	Y + Height; }
		set { Height = value - Y; }
	}

	public IntRect ( int x, int y, int width, int height ) {
		this.X = x;
		this.Y = y;
		this.Width = width;
		this.Height = height;
	}
}
