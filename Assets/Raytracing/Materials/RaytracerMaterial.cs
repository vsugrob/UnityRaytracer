using UnityEngine;
using System.Collections;

public abstract class RaytracerMaterial : MonoBehaviour {
	void Start () {}	// It is here to allow enable/disable component.

	public abstract Color GetColor ( Raytracer raytracer, Ray ray, RaycastHit hit, TraceData traceData );
}
