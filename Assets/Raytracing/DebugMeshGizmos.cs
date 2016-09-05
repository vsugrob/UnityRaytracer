using UnityEngine;
using System.Collections;

public class DebugMeshGizmos : MonoBehaviour {
	public float NormalLength = 0.1f;

	private MeshCollider _meshCollider;
	public MeshCollider MeshCollider {
		get {
			if ( _meshCollider == null )
				_meshCollider = GetComponent <MeshCollider> ();

			return	_meshCollider;
		}
	}

	void Update () {}

	void OnDrawGizmosSelected () {
		DrawGizmos ();
	}

	private void DrawGizmos () {
		if ( !enabled )
			return;

		if ( MeshCollider == null )
			return;

		var mesh = MeshCollider.sharedMesh;

		if ( mesh == null )
			return;

		Gizmos.color = Color.green;
		var vertices = mesh.vertices;
		var normals = mesh.normals;

		//print ( "vertices: " + mesh.vertices.Length );
		//print ( "normals: " + mesh.normals.Length );
		//print ( "triangles: " + mesh.triangles.Length );

		for ( int i = 0 ; i < vertices.Length ; i++ ) {
			Vector3 v = vertices [i];
			v = transform.TransformPoint ( v );
			Vector3 n = normals [i];
			n = transform.TransformDirection ( n );

			Gizmos.DrawLine ( v, v + n * NormalLength );
		}
	}
}
