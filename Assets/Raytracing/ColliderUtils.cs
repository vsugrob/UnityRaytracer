using UnityEngine;
using System.Collections;

public static class ColliderUtils {
	public static void GetTexCoordAndTangent (
		SphereCollider collider, Vector3 normal,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		localNormal = collider.transform.InverseTransformDirection ( normal );

		// Make vector components suitable for inverse trig functions.
		localNormal.x = Mathf.Clamp ( localNormal.x, -1, 1 );
		localNormal.y = Mathf.Clamp ( localNormal.y, -1, 1 );
		localNormal.z = Mathf.Clamp ( localNormal.z, -1, 1 );

		float u = Mathf.Atan2 ( localNormal.z, localNormal.x );
		u = Mathf.Clamp01 ( ( u + Mathf.PI ) / ( 2 * Mathf.PI ) );	// TODO: is clamping necessary?
		float v = Mathf.Asin ( localNormal.y );
		v = Mathf.Clamp01 ( ( v + Mathf.PI / 2 ) / Mathf.PI );		// TODO: is clamping necessary?
		texCoord = new Vector2 ( u, v );

		// Tangent is left perpendicular to projection of normal onto XZ plane.
		localTangent = new Vector3 ( -localNormal.z, 0, localNormal.x );
		localTangent.Normalize ();

		// Normalize () returns zero vector when magnitude was very low.
		if ( localTangent == Vector3.zero )
			localTangent = Vector3.right;
	}

	public static void GetTexCoordAndTangent (
		BoxCollider collider, Vector3 point, Vector3 normal,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		localNormal = collider.transform.InverseTransformDirection ( normal );
		Vector3 localPoint = collider.transform.InverseTransformPoint ( point );
		Vector3 size = collider.size;
		localPoint += size * 0.5f;
		texCoord = Vector2.zero;

		int sign = ( int ) localNormal.x;

		if ( sign != 0 ) {	// Right or left
			texCoord.x = localPoint.z / size.z;
			texCoord.y = localPoint.y / size.y;
			localTangent = new Vector3 ( 0, 0, sign );
		} else {
			sign = ( int ) localNormal.y;

			if ( sign != 0 ) {	// Top or bottom
				texCoord.x = localPoint.x / size.x;
				texCoord.y = localPoint.z / size.z;
				localTangent = new Vector3 ( sign, 0, 0 );
			} else {	// Front or back
				// Invert sign, treat side with localNormal (0, 0, -1) as front.
				sign = -( int ) localNormal.z;

				texCoord.x = localPoint.x / size.x;
				texCoord.y = localPoint.y / size.y;
				localTangent = new Vector3 ( sign, 0, 0 );
			}
		}

		if ( sign < 0 )
			texCoord.x = 1 - texCoord.x;
	}

	public static void GetTexCoordAndTangent (
		CapsuleCollider collider, Vector3 point, Vector3 normal,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		GetCapsuleTexCoordAndTangent (
			collider.transform, collider.height, collider.radius, point, normal,
			out texCoord, out localNormal, out localTangent
		);
	}

	public static void GetTexCoordAndTangent (
		CharacterController collider, Vector3 point, Vector3 normal,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		GetCapsuleTexCoordAndTangent (
			collider.transform, collider.height, collider.radius, point, normal,
			out texCoord, out localNormal, out localTangent
		);
	}

	public static void GetCapsuleTexCoordAndTangent (
		Transform transform, float height, float radius, Vector3 point, Vector3 normal,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		// Length of cylindrical segment of the capsule.
		float cylinderHeight = height - radius * 2;
		float sphereLength = radius * Mathf.PI / 2;
		float surfaceLength = sphereLength * 2 + cylinderHeight;
		float spherePortion = sphereLength / surfaceLength;
		
		localNormal = transform.InverseTransformDirection ( normal );

		// Make vector components suitable for inverse trig functions.
		localNormal.x = Mathf.Clamp ( localNormal.x, -1, 1 );
		localNormal.y = Mathf.Clamp ( localNormal.y, -1, 1 );
		localNormal.z = Mathf.Clamp ( localNormal.z, -1, 1 );

		float u = Mathf.Atan2 ( localNormal.z, localNormal.x );
		u = Mathf.Clamp01 ( ( u + Mathf.PI ) / ( 2 * Mathf.PI ) );
		float v = Mathf.Asin ( localNormal.y );
		const float CylindricalSegmentMaxAngle = 0.1f * Mathf.Deg2Rad;	// TODO: promote to class constants.

		if ( Mathf.Abs ( v ) < CylindricalSegmentMaxAngle ) {	// Point is on cylindrical segment.
			Vector3 localPoint = transform.InverseTransformPoint ( point );
			float cylinderPortion = cylinderHeight / surfaceLength;
			v = ( localPoint.y + cylinderHeight * 0.5f ) / cylinderHeight;
			v = v * cylinderPortion + spherePortion;
		} else {	// Point is on spherical segment.
			if ( v > 0 )
				v = spherePortion * v / ( Mathf.PI / 2 ) + 1 - spherePortion;
			else
				v = spherePortion * ( 1 + v / ( Mathf.PI / 2 ) );
		}

		texCoord = new Vector2 ( u, v );

		// Tangent is left perpendicular to projection of normal onto XZ plane.
		localTangent = new Vector3 ( -localNormal.z, 0, localNormal.x );
		localTangent.Normalize ();

		// Normalize () returns zero vector when magnitude was very low.
		if ( localTangent == Vector3.zero )
			localTangent = Vector3.right;
	}

	public static void GetTexCoordAndTangent (
		MeshCollider collider, RaycastHit hit,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		texCoord = hit.textureCoord;
		
		Vector3 bc = hit.barycentricCoordinate;
		//bc = Vector3.one / 2;	// TODO: make non-smooth normals as an option. Same applies to tangents.
		int triIndex = hit.triangleIndex;
		var mesh = collider.sharedMesh;
		int [] triangles = mesh.triangles;

		localNormal = InterpolateBarycentric ( triangles, mesh.normals, triIndex, bc ).normalized;
		//localNormal = collider.transform.InverseTransformDirection ( hit.normal );	// TODO: make non-smooth normals as an option. Same applies to tangents.
		localTangent = InterpolateBarycentric ( triangles, mesh.tangents, triIndex, bc );
		localTangent.Normalize ();
	}

	public static Vector3 InterpolateBarycentric ( int [] triangles, Vector3 [] values, int triIndex, Vector3 barycentricCoordinate ) {
		int vertexIndex = triIndex * 3;
		Vector3 v0 = values [triangles [vertexIndex]];
		Vector3 v1 = values [triangles [vertexIndex + 1]];
		Vector3 v2 = values [triangles [vertexIndex + 2]];

		return	v0 * barycentricCoordinate.x +
				v1 * barycentricCoordinate.y +
				v2 * barycentricCoordinate.z;
	}

	public static Vector4 InterpolateBarycentric ( int [] triangles, Vector4 [] values, int triIndex, Vector4 barycentricCoordinate ) {
		int vertexIndex = triIndex * 3;
		Vector4 v0 = values [triangles [vertexIndex]];
		Vector4 v1 = values [triangles [vertexIndex + 1]];
		Vector4 v2 = values [triangles [vertexIndex + 2]];

		return	v0 * barycentricCoordinate.x +
				v1 * barycentricCoordinate.y +
				v2 * barycentricCoordinate.z;
	}

	public static void GetTexCoordAndTangent (
		TerrainCollider collider, RaycastHit hit,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		texCoord = hit.textureCoord;

		var terrData = collider.terrainData;
		Vector3 localPoint = collider.transform.InverseTransformPoint ( hit.point );
		Vector3 terrSize = terrData.size;
		Vector2 terrCoord = new Vector2 ( localPoint.x / terrSize.x, localPoint.z / terrSize.z );
		localNormal = terrData.GetInterpolatedNormal ( terrCoord.x, terrCoord.y );
		//localNormal = collider.transform.InverseTransformDirection ( hit.normal );	// TODO: non-smooth normal (make it as an option).
		localTangent = Vector3.right;
	}

	public static void GetTexCoordAndTangent (
		RaycastHit hit,
		out Vector2 texCoord, out Vector3 localNormal, out Vector3 localTangent
	) {
		var collider = hit.collider;

		if ( collider is SphereCollider ) {
			GetTexCoordAndTangent (
				collider as SphereCollider, hit.normal,
				out texCoord, out localNormal, out localTangent
			);
		} else if ( collider is BoxCollider ) {
			GetTexCoordAndTangent (
				collider as BoxCollider, hit.point, hit.normal,
				out texCoord, out localNormal, out localTangent
			);
		} else if ( collider is CapsuleCollider ) {
			GetTexCoordAndTangent (
				collider as CapsuleCollider, hit.point, hit.normal,
				out texCoord, out localNormal, out localTangent
			);
		} else if ( collider is CharacterController ) {
			GetTexCoordAndTangent (
				collider as CharacterController, hit.point, hit.normal,
				out texCoord, out localNormal, out localTangent
			);
		} else if ( collider is MeshCollider ) {
			GetTexCoordAndTangent (
				collider as MeshCollider, hit,
				out texCoord, out localNormal, out localTangent
			);
		} else if ( collider is TerrainCollider ) {
			GetTexCoordAndTangent (
				collider as TerrainCollider, hit,
				out texCoord, out localNormal, out localTangent
			);
		} else {
			texCoord = Vector2.zero;
			localNormal = collider.transform.InverseTransformDirection ( hit.normal );
			localTangent = Vector3.right;
		}
		
		/* TODO: collider list to implement:
		 * BoxCollider - done.
		 * CapsuleCollider - done.
		 * CharacterController - done.
		 * MeshCollider - done.
		 * SphereCollider - done.
		 * TerrainCollider - done.
		 * WheelCollider - no need to implement this, it's not directly visualizable. I'm not even sure whether it is raycastable. */
	}
}
