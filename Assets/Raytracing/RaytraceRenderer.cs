using UnityEngine;
using System.Collections;
using System.IO;
using System;
using UnityEditor;
using System.Linq;

[RequireComponent ( typeof ( Raytracer ) )]
[AddComponentMenu ( "Raytracing/Raytrace Renderer" )]
public class RaytraceRenderer : MonoBehaviour {
	public int NumFramesToRender = 1;
	public float FramesPerSecond = 10;
	public int Width = 1024;
	public int Height = 768;
	public bool RenderPortionOfImage = false;
	public IntRect PortionRect = new IntRect ( 0, 0, -1, -1 );
	private Texture2D outputTexture;
	public Texture2D OutputTexture { get { return	outputTexture; } }
	private int numFramesRendered = 0;
	public int NumFramesRendered { get { return	numFramesRendered; } }
	private Raytracer raytracer;
	public Raytracer Raytracer { get { return	raytracer; } }
	private string OutputDir = "Output";

	private float lastFrameTime = float.NegativeInfinity;
	private float frameInterval;

	private Camera _camera;
	public Camera Camera {
		get {
			if ( _camera == null )
				_camera = GetComponent <Camera> ();

			return	_camera;
		}
	}

	void Awake () {
		InitDirectories ();
		frameInterval = 1 / FramesPerSecond;
	}

	private void InitOutputTexture () {
		if ( outputTexture != null )
			DestroyImmediate ( outputTexture );

		int w, h;

		if ( RenderPortionOfImage ) {
			w = PortionRect.Width < 0 ? Width : PortionRect.Width;
			h = PortionRect.Height < 0 ? Height : PortionRect.Height;
		} else {
			w = Width;
			h = Height;
		}

		outputTexture = AllocTexture ( w, h );
	}

	private Texture2D AllocTexture ( int width, int height ) {
		if ( width <= 0 )
			throw new ArgumentException ( "Width must be greater than zero.", "width" );
		else if ( height <= 0 )
			throw new ArgumentException ( "Height must be greater than zero.", "height" );

		return	new Texture2D ( width, height, TextureFormat.ARGB32, false );
	}

	void Start () {
		raytracer = GetComponent <Raytracer> ();
	}
	
	void Update () {
		if ( numFramesRendered >= NumFramesToRender )
			return;

		if ( Time.fixedTime - lastFrameTime < frameInterval )
			return;
		else
			lastFrameTime = Time.fixedTime;

		Render ();

		int numDigits = ( int ) Math.Ceiling ( Math.Log10 ( NumFramesToRender ) );
		string path = Path.Combine (
			OutputDir,
			string.Format ( "frame_{0}.png", numFramesRendered.ToString ( "D" + numDigits ) )
		);
		WriteToFile ( path );

		numFramesRendered++;

		if ( numFramesRendered == NumFramesToRender )
			EditorApplication.Beep ();
	}

	public void Render () {
		InitOutputTexture ();

		IntRect? rectToRender = null;

		if ( RenderPortionOfImage )
			rectToRender = PortionRect;

		raytracer.Render ( outputTexture, Width, Height, rectToRender );
	}

	[ContextMenu ( "Render Single Frame" )]
	void RenderSingleFrameFromContextMenu () {
		Awake ();
		Start ();

		Render ();
		string path = Path.Combine ( OutputDir, "out.png" );
		WriteToFile ( path );

		var window = RaytracerOutputWindow.GetInstance ( false );
		window.Repaint ();

		EditorApplication.Beep ();
	}

	[MenuItem ( "Raytracing/Render _#F5" )]
	static void RenderSingleFrameFromApplicationMenu () {
		var renderer = FindObjectOfType <RaytraceRenderer> ();

		if ( renderer != null )
			renderer.RenderSingleFrameFromContextMenu ();
		else
			Debug.LogWarning ( "Couldn't find camera with RaytraceRenderer component enabled on it." );
	}

	public void WriteToFile ( string path ) {
		var bytes = outputTexture.EncodeToPNG ();
		File.WriteAllBytes ( path, bytes );
	}

	void OnDrawGizmos () {
		if ( GizmoSettings.AlwaysDrawGizmos )
			DrawGizmos ();
	}

	void OnDrawGizmosSelected () {
		DrawGizmos ();
	}

	private void DrawGizmos () {
		GizmoSettings.Validate ();
		
		float aspect = ( float ) Width / Height;

		if ( GizmoSettings.DrawFrustum ) {
			Matrix4x4 originalMatrix = Gizmos.matrix;
			Gizmos.matrix = Matrix4x4.TRS ( transform.position, transform.rotation, transform.localScale );
			Gizmos.color = Color.gray;

			if ( Camera.orthographic ) {
				float orthoSize = Camera.orthographicSize * 2;
				Vector3 cubeSize = new Vector3 (
					orthoSize * aspect,
					orthoSize,
					Camera.farClipPlane - Camera.nearClipPlane
				);

				Vector3 cubePos = new Vector3 (
					0,
					0,
					cubeSize.z * 0.5f + Camera.nearClipPlane
				);
				
				Gizmos.DrawWireCube ( cubePos, cubeSize );
			} else
				Gizmos.DrawFrustum ( Vector3.zero, Camera.fieldOfView, Camera.farClipPlane, Camera.nearClipPlane, aspect );

			Gizmos.matrix = originalMatrix;
		}

		if ( GizmoSettings.DrawTracePaths ) {
			Camera.aspect = aspect;
			Awake ();
			Start ();

			float halfPixelWidth = 1.0f / GizmoSettings.NumPathsX / 2;
			float halfPixelHeight = 1.0f / GizmoSettings.NumPathsY / 2;
			var counters = new RaytraceCounters ();

			for ( int y = 0 ; y < GizmoSettings.NumPathsY ; y++ ) {
				for ( int x = 0 ; x < GizmoSettings.NumPathsX ; x++ ) {
					counters.InitialRays++;

					Ray ray = Camera.ViewportPointToRay (
						new Vector3 (
							( float ) x / GizmoSettings.NumPathsX + halfPixelWidth,
							( float ) y / GizmoSettings.NumPathsY + halfPixelHeight,
							0
						)
					);

					TraceData traceData = new TraceData ( Camera.backgroundColor, counters, recordHistory: true );
					raytracer.Trace ( ray, traceData );

					var flatBranches = traceData.FlattenBranches ().ToArray ();
					const float NoHitSegmentLength = 10;	// TODO: move it to argument of function that draws segments and/or make it come from setting.
#if PERIODIC_UPDATE
					const float SpiderSpawnRate = 0.5f; // Number of spiders spawning per second.
					const float SpiderVelocity = 1;		// Units per second.
					const float SpiderLength = 0.1f;
					float spiderSpawnInterval = 1 / SpiderSpawnRate;
#endif

					foreach ( var td in flatBranches ) {
						float segStartDistance = td.StartDistance;

						foreach ( var item in td.History ) {
							Ray itemRay = item.Ray;
							Color rayColor = GizmoSettings.UseCustomColorForPaths ? GizmoSettings.PathsColor : item.Color;
							Gizmos.color = rayColor;
							float segEndDistance;

							if ( item.Hit.HasValue ) {
								RaycastHit hit = item.Hit.Value;
								Gizmos.DrawLine ( itemRay.origin, hit.point );
								segEndDistance = segStartDistance + hit.distance;
							} else {
								Gizmos.DrawLine ( itemRay.origin, itemRay.GetPoint ( NoHitSegmentLength ) );
								segEndDistance = segStartDistance + NoHitSegmentLength;
							}

#if PERIODIC_UPDATE
							/* Draw marching ants crawling along the rays */
							/* TODO: I couldn't figure out how to make SceneView update periodically
							 * rather than in response to events. I don't know where from I can call
							 * SceneView.RepaintAll () in order to force scene to update itself as well. */
							float timeToReachStart = segStartDistance / SpiderVelocity;
							float timeToReachEnd = segEndDistance / SpiderVelocity;
							float segTravelTime = timeToReachEnd - timeToReachStart;
							float t = ( ( float ) EditorApplication.timeSinceStartup + timeToReachStart ) % spiderSpawnInterval;

							for ( ; t < segTravelTime ; t += spiderSpawnInterval ) {
								Gizmos.color = new Color ( 1 - rayColor.r, 1 - rayColor.g, 1 - rayColor.b, 1 );
								Vector3 spiderPos = itemRay.GetPoint ( t * SpiderVelocity );
								Gizmos.DrawLine ( spiderPos, spiderPos + itemRay.direction * SpiderLength );
							}
#endif

							segStartDistance = segEndDistance;
						}
					}
				}
			}

			Camera.ResetAspect ();
		}
	}

	private void InitDirectories () {
		OutputDir = Path.Combine ( Path.Combine ( Application.dataPath, "../" ), OutputDir );

		if ( !Directory.Exists ( OutputDir ) )
			Directory.CreateDirectory ( OutputDir );
	}

	[System.Serializable]
	public class GizmoSettingsGroup {
		public const int MaxNumPathsX = 60;
		public const int MaxNumPathsY = 60;
		public bool AlwaysDrawGizmos = false;
		public bool DrawFrustum = true;
		public bool DrawTracePaths = true;
		public int NumPathsX = 9;
		public int NumPathsY = 1;
		public bool UseCustomColorForPaths = false;
		public Color PathsColor = new Color ( 1, 140f / 255, 0 );

		public void Validate () {
			NumPathsX = Mathf.Clamp ( NumPathsX, 1, MaxNumPathsX );
			NumPathsY = Mathf.Clamp ( NumPathsY, 1, MaxNumPathsY );
		}
	}

	public GizmoSettingsGroup GizmoSettings = new GizmoSettingsGroup ();
}
