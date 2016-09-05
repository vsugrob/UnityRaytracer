using UnityEngine;
using System.Collections;
using UnityEditor;
using System;

public class RaytracerOutputWindow : EditorWindow {
	static readonly TimeSpan RepaintInterval = TimeSpan.FromSeconds ( 0.5 );
	static readonly TimeSpan SceneViewRepaintInterval = TimeSpan.FromSeconds ( 0.03 );
	private DateTime lastRepaintTime = DateTime.MinValue;
	private DateTime lastSceneViewRepaintTime = DateTime.MinValue;

	[MenuItem ( "Raytracing/Raytracer Output" )]
	[MenuItem ( "Window/Raytracer Output" )]
	public static void ShowOutputWindow () {
		GetInstance ( true );
	}

	public static RaytracerOutputWindow GetInstance ( bool focus = true ) {
		Type gameViewType = Type.GetType ( "UnityEditor.GameView,UnityEditor" );
		var window = EditorWindow.GetWindow <RaytracerOutputWindow> ( "Output", focus, gameViewType );
		window.Show ();

		return	window;
	}

	void OnGUI () {
		var renderer = FindObjectOfType <RaytraceRenderer> ();
		
		if ( renderer != null ) {
			if ( renderer.OutputTexture != null ) {
				var raytracer = renderer.Raytracer;
				float raycastsPerSec = raytracer.Counters.TotalRaycasts / raytracer.LastRenderTime;
				GUILayout.Label ( "Frame: " + ( renderer.NumFramesRendered + 1 ) + "/" + renderer.NumFramesToRender );
				GUILayout.Label ( "Time elapsed: " + raytracer.LastRenderTime + " sec" );
				GUILayout.Label ( "Raycasts per sec: " + raycastsPerSec + " (" + ( 1 / raycastsPerSec ) + " sec per cast)" );
				GUILayout.Label (
					"Raycasts: " + raytracer.Counters.TotalRaycasts +
					" (forward: " + raytracer.Counters.Raycasts +
					", backward: " + raytracer.Counters.Backtraces + ")"
				);
				GUILayout.Label (
					"Initial rays: " + raytracer.Counters.InitialRays +
					" (" + renderer.Width + "x" + renderer.Height + ")"
				);
				GUILayout.Label (
					"Reflections: " + raytracer.Counters.TotalReflections +
					" (outer: " + raytracer.Counters.Reflections +
					", inner: " + raytracer.Counters.InnerReflections + ")"
				);
				GUILayout.Label ( "Refractions: " + raytracer.Counters.Refractions );
				GUILayout.Label (
					"Overwhites: " +
					( raytracer.StopOnOverwhite ? raytracer.Counters.Overwhites.ToString () : "Not counted" )
				);
				GUILayout.Label ( renderer.OutputTexture );
			} else
				GUILayout.Label ( "Nothing to display: the scene wasn't rendered yet." );
		} else
			GUILayout.Label ( "Couldn't find any camera with RaytraceRenderer component on it." );
	}

	void Update () {
		if ( DateTime.Now - lastRepaintTime > RepaintInterval ) {
			Repaint ();
			lastRepaintTime = DateTime.Now;
		}

		if ( DateTime.Now - lastSceneViewRepaintTime > SceneViewRepaintInterval ) {
			// DEVEL: trick to make scene view repainted on interval basis.
			//SceneView.RepaintAll ();
			lastSceneViewRepaintTime = DateTime.Now;
		}
	}
}
