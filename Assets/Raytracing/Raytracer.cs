using UnityEngine;
using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

[RequireComponent ( typeof ( Camera ) )]
[AddComponentMenu ( "Raytracing/Raytracer" )]
public class Raytracer : MonoBehaviour {
	public const float PushOutMagnitude = 0.0001f;
	private const float MinRaycastDistance = 0.01f;
	public int MaxReflections = 10;
	public int MaxRefractions = 10;
	public int MaxInnerReflections = 1;
	public bool StopOnOverwhite = true;
	public RaytracerMaterial DefaultMaterial;
	public bool OverrideAmbientLight = false;
	public Color AmbientLight = new Color ( 0.2f, 0.2f, 0.2f, 1 );

	public RaytraceCounters Counters = new RaytraceCounters ();
	private float lastRenderTime = 0;
	public float LastRenderTime { get { return	lastRenderTime; } }

	private Camera _camera;
	public Camera Camera {
		get {
			if ( _camera == null )
				_camera = GetComponent <Camera> ();

			return	_camera;
		}
	}

	void Update () {
		//RefractionTest ();
	}

	public void Render (
		Texture2D outputTexture,
		int resolutionWidth, int resolutionHeight,
		IntRect? rectToRender = null
	) {
		if ( !rectToRender.HasValue )
			rectToRender = new IntRect ( 0, 0, -1, -1 );

		var rect = rectToRender.Value;

		if ( rect.X < 0 || rect.X >= resolutionWidth )
			throw new ArgumentException ( "rect.X must fall in bounds from 0 to resolutionWidth", "rect.X" );

		if ( rect.Y < 0 || rect.Y >= resolutionWidth )
			throw new ArgumentException ( "rect.Y must fall in bounds from 0 to resolutionHeight", "rect.Y" );
		
		if ( rect.Width < 0 )
			rect.Width = resolutionWidth - rect.X;
		else if ( rect.Right > resolutionWidth )
			rect.Right = resolutionWidth;

		if ( rect.Height < 0 )
			rect.Height = resolutionHeight - rect.Y;
		else if ( rect.Bottom > resolutionHeight )
			rect.Bottom = resolutionHeight;

		Camera.aspect = ( float ) resolutionWidth / resolutionHeight;
		Counters = new RaytraceCounters ();
		float halfPixelWidth = 1.0f / resolutionWidth / 2;
		float halfPixelHeight = 1.0f / resolutionHeight / 2;
		float timeStart = Time.realtimeSinceStartup;

		for ( int sy = rect.Y ; sy < rect.Bottom ; sy++ ) {
			for ( int sx = rect.X ; sx < rect.Right ; sx++ ) {
				Counters.InitialRays++;

				Vector3 viewportPoint = new Vector3 (
					( float ) sx / resolutionWidth + halfPixelWidth,
					( float ) sy / resolutionHeight + halfPixelHeight,
					0
				);
				Ray ray = Camera.ViewportPointToRay ( viewportPoint );
				var traceData = new TraceData ( Camera.backgroundColor, Counters );
				Color color = Trace ( ray, traceData );
				
				outputTexture.SetPixel ( sx - rect.X, sy - rect.Y, color );
			}
		}
		
		outputTexture.Apply ();
		lastRenderTime = Time.realtimeSinceStartup - timeStart;
		Camera.ResetAspect ();
	}

	public Color Trace ( Ray ray, TraceData traceData ) {
		RaycastHit forwardHit;
		Color color;

		traceData.Counters.Raycasts++;

		if ( Physics.Raycast ( ray, out forwardHit ) ) {
			if ( traceData.PenetrationStack.Count != 0 ) {
				Vector3 pushedOutOrigin = forwardHit.point + forwardHit.normal * PushOutMagnitude;
				Ray backwardRay = new Ray ( pushedOutOrigin, -ray.direction );

				if ( TraceBackward ( ray, backwardRay, forwardHit.distance, traceData, out color ) )
					return	color;
			}

			color = GetColor ( ray, forwardHit, traceData );
			traceData.AddHistoryItem ( ray, forwardHit, color );

			return	color;
		} else {
			if ( traceData.PenetrationStack.Count != 0 ) {
				var soughtCollider = traceData.PenetrationStack.Peek ().collider;
				var bounds = soughtCollider.bounds;

				Vector3 extents = bounds.extents;
				float boundingSphereRadius = extents.magnitude;
				Vector3 vToBbCenter = bounds.center - ray.origin;
				float projLen = Vector3.Dot ( vToBbCenter, ray.direction );

				Vector3 pushedOutOrigin = ray.origin + ray.direction * ( projLen + boundingSphereRadius + PushOutMagnitude );
				Ray backwardRay = new Ray ( pushedOutOrigin, -ray.direction );

				if ( TraceBackward ( ray, backwardRay, Vector3.Distance ( ray.origin, pushedOutOrigin ), traceData, out color ) )
					return	color;
			}

			traceData.AddHistoryItem ( ray, null, traceData.BackgroundColor );

			return	traceData.BackgroundColor;
		}
	}

	private bool TraceBackward ( Ray forwardRay, Ray backwardRay, float maxDistance, TraceData traceData, out Color color ) {
		var soughtCollider = traceData.PenetrationStack.Peek ().collider;

		traceData.Counters.Backtraces++;

		/* Sometimes, due to imprecision of calculations,
		 * maxDistance is less than or equal to zero.
		 * We can fix this issue by setting it to some small value,
		 * it will not cause any error since origin point and
		 * ray direction are fine. */
		if ( maxDistance < MinRaycastDistance )
			maxDistance = MinRaycastDistance;

		var hits = Physics.RaycastAll ( backwardRay, maxDistance )
			.OrderByDescending ( h => h.distance )
			.Where (
				h => h.collider == soughtCollider &&
				h.distance < maxDistance	// TODO: necessary? We already requested hits not farther than maxDistance. Test it.
			)
			.ToArray ();

		if ( hits.Length != 0 ) {
			RaycastHit backwardHit = hits [0];
			backwardHit.distance = maxDistance - backwardHit.distance;
			color = GetColor ( forwardRay, backwardHit, traceData );

			traceData.AddHistoryItem ( forwardRay, backwardHit, color );

			return	true;
		} else {
			color = default ( Color );

			return	false;
		}
	}

	private Color GetColor ( Ray ray, RaycastHit hit, TraceData traceData ) {
		var material = hit.transform.GetComponent <RaytracerMaterial> ();
		Color color;

		if ( material != null && material.enabled )
			color = material.GetColor ( this, ray, hit, traceData );
		else
			color = DefaultMaterial.GetColor ( this, ray, hit, traceData );

		color.a = 1;	// TODO: is it really needed?

		return	color;
	}

	public static bool IsOverwhite ( Color c ) {
		return	c.r >= 1 && c.g >= 1 && c.b >= 1;
	}

	public bool MustInterrupt ( Color c, TraceData traceData ) {
		if ( StopOnOverwhite && Raytracer.IsOverwhite ( c ) ) {
			traceData.Counters.Overwhites++;

			return	true;
		} else
			return	false;
	}

	public static Vector3 Refract ( Vector3 incidentVector, Vector3 n, float nDotV, float k ) {
		float cosF = Mathf.Sqrt ( 1 - k * k * ( 1 - nDotV * nDotV ) );
		nDotV = -nDotV;
			
		if ( nDotV >= 0 )
			return	n * ( k * nDotV - cosF ) + incidentVector * k;
		else
			return	n * ( k * nDotV + cosF ) + incidentVector * k;
	}

	public static Vector3 Refract ( Vector3 incidentVector, Vector3 n, float k ) {
		return	Refract ( incidentVector, n, Vector3.Dot ( n, incidentVector ), k );
	}

	/* NOTE: this is my version of Refract. I just didn't get traditional formulas yet...
	 * My version produces results same as traditional, though it slightly heavier in calculations.
	 * incidentDir argument should be normalized. */
	public static Vector3 Refract2 ( Vector3 incidentDir, Vector3 normal, float k ) {
		float iDotN = Vector3.Dot ( incidentDir, normal );
		Vector3 perp = incidentDir - iDotN * normal;
		perp *= k;
		float normMag = Mathf.Sqrt ( 1 - perp.sqrMagnitude );
		Vector3 refr = perp + Mathf.Sign ( iDotN ) * normMag * normal;

		return	refr.normalized;
	}

	public static Vector3 TransformInverseTbn ( Vector3 vector, Vector3 tangent, Vector3 binormal, Vector3 normal ) {
		return	new Vector3 (
			Vector3.Dot ( vector, tangent ),
			Vector3.Dot ( vector, binormal ),
			Vector3.Dot ( vector, normal )
		);
	}

	public static Vector3 TransformTbn ( Vector3 vector, Vector3 tangent, Vector3 binormal, Vector3 normal ) {
		return	new Vector3 (
			vector.x * tangent.x + vector.y * binormal.x + vector.z * normal.x,
			vector.x * tangent.y + vector.y * binormal.y + vector.z * normal.y,
			vector.x * tangent.z + vector.y * binormal.z + vector.z * normal.z
		);
	}
	
	private static void RefractionTest () {
		float k = 1.3f;

		float CoefficientOut = k;
		float CoefficientIn = 1 / k;
		float CriticalOutAngleCos, CriticalInAngleCos;

		if ( CoefficientOut > 1 ) {
			CriticalOutAngleCos = Mathf.Sqrt ( 1 - CoefficientIn * CoefficientIn );
			CriticalInAngleCos = 0;
		} else {
			CriticalOutAngleCos = 0;
			CriticalInAngleCos = Mathf.Sqrt ( 1 - CoefficientOut * CoefficientOut );
		}

		float a = Mathf.Deg2Rad * ( -45 - 3 * Time.fixedTime );
		Vector3 normal = new Vector3 ( 0, 1, 0 );
		Vector3 incidentDir = new Vector3 ( Mathf.Cos ( a ), Mathf.Sin ( a ), 0 );
		float iDotN = Vector3.Dot ( incidentDir, normal );
		Vector3 r1 = Refract ( incidentDir, normal, k );
		Vector3 r2 = Refract2 ( incidentDir, normal, k );

		print (
			"r1: " + r1.ToString ( "R" ) + ", len: " + r1.magnitude +
			", r2: " + r2.ToString ( "R" ) + ", len: " + r2.magnitude
		);
		print (
			"iDotN: " + iDotN +
			", CriticalOutAngleCos: " + CriticalOutAngleCos +
			", CriticalInAngleCos: " + CriticalInAngleCos
		);

		float duration = 0;	//float.PositiveInfinity;
		Debug.DrawLine ( Vector3.left * 10, Vector3.right * 10, Color.gray, duration );
		Debug.DrawLine ( Vector3.up * 10, Vector3.down * 10, Color.gray, duration );

		Debug.DrawLine ( Vector3.zero, normal, Color.green, duration );
		Debug.DrawLine ( -incidentDir, Vector3.zero, Color.red, duration );
		Debug.DrawLine ( Vector3.zero, r1, Color.blue, duration );
		Debug.DrawLine ( Vector3.zero, r2, Color.yellow, duration );
	}
}

public class TraceData {
	public Color BackgroundColor;
	public bool RecordHistory;
	public RaytraceCounters Counters;
	public int NumReflections;
	public int NumInnerReflections;
	public int NumRefractions;
	public int Step;
	public float StartDistance;
	public Stack <RaycastHit> PenetrationStack = new Stack <RaycastHit> ();
	// TODO: rename History to TracePath and TraceHistoryItem to TracePathStep?
	// TODO: use LinkedList instead of list in order to minimize memory reallocations?
	public List <TraceHistoryItem> History = new List <TraceHistoryItem> ();
	public List <TraceData> Branches = new List <TraceData> ();

	public TraceData ( Color backgroundColor, RaytraceCounters counters, bool recordHistory = false ) {
		this.BackgroundColor = backgroundColor;
		this.Counters = counters;
		this.RecordHistory = recordHistory;
	}

	public void AddHistoryItem ( Ray ray, RaycastHit? hit, Color color ) {
		if ( !RecordHistory )
			return;
		
		History.Insert (
			History.Count,
			new TraceHistoryItem ( ray, hit, color, Step++ )
		);
	}

	public TraceData Fork () {
		var td = new TraceData ( BackgroundColor, Counters, RecordHistory ) {
			NumReflections = NumReflections,
			NumInnerReflections = NumInnerReflections,
			NumRefractions = NumRefractions,
			Step = Step + 1,
			StartDistance = CalculateHistoryDistance (),
			PenetrationStack = new Stack <RaycastHit> ( PenetrationStack.Reverse () )
		};

		Branches.Add ( td );

		return	td;
	}

	public float CalculateHistoryDistance ( float noHitSegmentLength = 0 ) {
		return	History.Sum ( item => item.Hit.HasValue ? item.Hit.Value.distance : noHitSegmentLength );
	}

	public IEnumerable <TraceData> FlattenBranches () {
		yield return	this;

		foreach ( var td in Branches ) {
			foreach ( var subTd in td.FlattenBranches () ) {
				yield return	subTd;
			}
		}
	}
}

public class TraceHistoryItem {
	public Ray Ray;
	public RaycastHit? Hit;
	public Color Color;
	public int Step;

	public TraceHistoryItem ( Ray ray, RaycastHit? hit, Color color, int step ) {
		this.Ray = ray;
		this.Hit = hit;
		this.Color = color;
		this.Step = step;
	}
}

public class RaytraceCounters {
	public int Raycasts;
	public int Backtraces;
	public int TotalRaycasts { get { return	Raycasts + Backtraces; } }
	public int InitialRays;
	public int Reflections;
	public int InnerReflections;
	public int TotalReflections { get { return	Reflections + InnerReflections; } }
	public int Refractions;
	public int Overwhites;
}