using UnityEngine;
using System.Collections;

// TODO: rename to GenericMaterial.
[AddComponentMenu ( "Raytracing/Compound Material" )]
public class CompoundMaterial : RaytracerMaterial {
	public Texture2D NormalMap;
	public float NormalMapInfluence = 1.0f;
	public Color DiffuseColor = Color.gray;
	public float DiffuseComponent = 1.0f;
	public Texture2D DiffuseTexture;
	public bool DiffuseColorIsBackground = true;
	public float SpecularComponent = 0.0f;
	public float SpecularPower = 5f;
	public Texture2D SpecularMap;
	public float SpecularMapInfluence = 1.0f;
	public float ReflectionComponent = 0.0f;
	public float InnerReflectionComponent = 0.0f;
	public Texture2D ReflectionMap;
	public float ReflectionMapInfluence = 1.0f;
	public float RefractionComponent = 0.0f;
	public float RefractionIndex =  1.34312f;	// H20
	public bool RefractWhereTranslucent = false;
	public float ColorAberration = 0.0f;
	private float LightIntensityFactor = 2f;
	private float DiffuseExponent = 1.5f;

	private float CoefficientIn;
	private float CoefficientOut;
	private float CriticalOutAngleCos;
	private float CriticalInAngleCos;

	public override Color GetColor ( Raytracer raytracer, Ray ray, RaycastHit hit, TraceData traceData ) {
		Vector2 texCoord;
		Vector3 surfaceNormal;
		Vector3 tangent, binormal;
		bool texCoordAndTangentRequired = NormalMap != null || DiffuseTexture != null || SpecularMap != null || ReflectionMap != null;
		texCoordAndTangentRequired = true;	// DEBUG

		if ( texCoordAndTangentRequired ) {
			Vector3 localNormal;
			Vector3 localTangent;

			ColliderUtils.GetTexCoordAndTangent (
				hit,
				out texCoord, out localNormal, out localTangent
			);

			tangent = hit.collider.transform.TransformDirection ( localTangent );
			surfaceNormal = hit.collider.transform.TransformDirection ( localNormal );
			binormal = Vector3.Cross ( tangent, surfaceNormal );

			#region Debug Visualisation
#if DEBUG_SHOW_TEX_COORDS
			return	new Color ( texCoord.x, texCoord.y, 0, 1 );
#endif

#if DEBUG_SHOW_BARYCENTRIC_COORDS
			Vector3 dbgBc = hit.barycentricCoordinate;

			return	new Color ( dbgBc.x, dbgBc.y, dbgBc.z, 1 );
#endif

#if DEBUG_SHOW_NORMALS
			Vector3 dbgLocalNormal = localNormal;
			//dbgLocalNormal = new Vector3 (
			//	Mathf.Abs ( dbgLocalNormal.x ),
			//	Mathf.Abs ( dbgLocalNormal.y ),
			//	Mathf.Abs ( dbgLocalNormal.z )
			//);
			dbgLocalNormal = ( dbgLocalNormal + Vector3.one ) * 0.5f;

			return	new Color ( dbgLocalNormal.x, dbgLocalNormal.y, dbgLocalNormal.z, 1 );
#endif

#if DEBUG_SHOW_TANGENTS
			Vector3 dbgLocalTangent = localTangent;

			if ( false ) {
				dbgLocalTangent = new Vector3 (
					Mathf.Abs ( dbgLocalTangent.x ),
					Mathf.Abs ( dbgLocalTangent.y ),
					Mathf.Abs ( dbgLocalTangent.z )
				);
			} else if ( false ) {
				dbgLocalTangent = new Vector3 (
					Mathf.Abs ( dbgLocalTangent.x > 0 ? dbgLocalTangent.x : 0 ),
					Mathf.Abs ( dbgLocalTangent.y > 0 ? dbgLocalTangent.y : 0 ),
					Mathf.Abs ( dbgLocalTangent.z > 0 ? dbgLocalTangent.z : 0 )
				);
			} else {
				dbgLocalTangent = ( dbgLocalTangent + Vector3.one ) * 0.5f;
			}

			return	new Color ( dbgLocalTangent.x, dbgLocalTangent.y, dbgLocalTangent.z, 1 );
#endif

#if DEBUG_SHOW_BINORMALS
			Vector3 localBinormal = Vector3.Cross ( localTangent, localNormal );
			Vector3 dbgBinormal = localBinormal;
			dbgBinormal = new Vector3 (
				Mathf.Abs ( dbgBinormal.x ),
				Mathf.Abs ( dbgBinormal.y ),
				Mathf.Abs ( dbgBinormal.z )
			);

			return	new Color ( dbgBinormal.x, dbgBinormal.y, dbgBinormal.z, 1 );
#endif
#endregion Debug Visualisation
		} else {
			texCoord = Vector2.zero;
			surfaceNormal = hit.normal;
			tangent = Vector3.zero;
			binormal = Vector3.zero;
		}

		bool entering = Vector3.Dot ( hit.normal, ray.direction ) <= 0;
		surfaceNormal = entering ? surfaceNormal : -surfaceNormal;
		/* TODO: revise where "entering" calculated upon hit.normal should be replaced
		 * with "entering" calculated upon surfaceNormal transformed with TBN. */

		if ( NormalMap != null && NormalMapInfluence > 0 ) {
			var normalMapRt = TextureCache.FromUnityTexture ( NormalMap );
			Color normalMapColor = normalMapRt.GetFilteredPixel ( texCoord.x, texCoord.y );
			Vector3 texNormal = new Vector3 ( normalMapColor.r, normalMapColor.g, normalMapColor.b );
			texNormal = 2 * texNormal - Vector3.one;
			texNormal.Normalize ();
			Vector3 texNormalWorld = Raytracer.TransformTbn ( texNormal, tangent, binormal, surfaceNormal );

			float normalMapInfluence = Mathf.Clamp01 ( NormalMapInfluence );
			surfaceNormal = Vector3.Lerp ( surfaceNormal, texNormalWorld, normalMapInfluence ).normalized;

#if DEBUG_SHOW_SURFACE_NORMALS
			Vector3 dbgSurfaceNormal = surfaceNormal;
			//dbgSurfaceNormal = new Vector3 (
			//	Mathf.Abs ( dbgSurfaceNormal.x ),
			//	Mathf.Abs ( dbgSurfaceNormal.y ),
			//	Mathf.Abs ( dbgSurfaceNormal.z )
			//);
			dbgSurfaceNormal = ( dbgSurfaceNormal + Vector3.one ) * 0.5f;

			return	new Color ( dbgSurfaceNormal.x, dbgSurfaceNormal.y, dbgSurfaceNormal.z, 1 );
#endif
		}

		float specularIntensity = SpecularComponent;

		if ( SpecularMap != null && SpecularMapInfluence > 0 && specularIntensity > 0 ) {
			var specularMapRt = TextureCache.FromUnityTexture ( SpecularMap );
			Color specularMapColor = specularMapRt.GetFilteredPixel ( texCoord.x, texCoord.y );
			specularIntensity *= specularMapColor.grayscale * specularMapColor.a;

			float specularMapInfluence = Mathf.Clamp01 ( SpecularMapInfluence );
			specularIntensity = Mathf.Lerp ( SpecularComponent, specularIntensity, specularMapInfluence );
		}
		
		Color totalColor = Color.black;
		Color diffuseLightSumColor = raytracer.OverrideAmbientLight ? raytracer.AmbientLight : RenderSettings.ambientLight;
		diffuseLightSumColor *= DiffuseComponent;
		Color specularLightSumColor = Color.black;

		var lights = Light.GetLights ( LightType.Point, 0 );

		foreach ( var light in lights ) {
			Vector3 vToLight = light.transform.position - hit.point;
			float lightVolumeRadius = light.range * 0.00625f;	// Empirical coefficient.
			float distance = vToLight.magnitude - lightVolumeRadius;

			if ( distance < 0 )
				distance = 0;
			else if ( distance >= light.range )
				continue;

			Vector3 dirToLight = vToLight.normalized;
			float attenuation;
			attenuation = 1 - distance / light.range;
			attenuation = attenuation * attenuation;

			float lightIntensity = light.intensity * LightIntensityFactor;

			if ( DiffuseComponent > 0 ) {
				float diffuseIntensity = Vector3.Dot ( dirToLight, surfaceNormal );

				if ( diffuseIntensity > 0 ) {
					diffuseIntensity = Mathf.Pow ( diffuseIntensity, DiffuseExponent );
					Color diffuseLightColor = light.color * attenuation * diffuseIntensity * lightIntensity;
					diffuseLightSumColor += diffuseLightColor;
				}
			}

			if ( specularIntensity > 0 ) {
				Vector3 reflectionDir = Vector3.Reflect ( -dirToLight, surfaceNormal );
				Vector3 vToView = raytracer.Camera.transform.position - hit.point;
				Vector3 dirToView = vToView.normalized;
				float specularity = Vector3.Dot ( reflectionDir, dirToView );

				if ( specularity > 0 ) {
					specularity = Mathf.Pow ( specularity, SpecularPower );
					Color specularLightColor = light.color * attenuation * specularity * lightIntensity;
					specularLightSumColor += specularLightColor;
				}
			}
		}

		Color diffuseColor;

		if ( DiffuseTexture != null ) {
			var diffuseTextureRt = TextureCache.FromUnityTexture ( DiffuseTexture );
			// TODO: calculate miplevel, get the color according to its value.
			Color texColor = diffuseTextureRt.GetFilteredPixel ( texCoord.x, texCoord.y );
			
			if ( texColor.a < 1 && DiffuseColorIsBackground )
				diffuseColor = Color.Lerp ( this.DiffuseColor, texColor, texColor.a );
			else
				diffuseColor = Color.Lerp ( Color.black, texColor, texColor.a );

			diffuseColor.a = texColor.a;
		} else
			diffuseColor = this.DiffuseColor;

		totalColor = diffuseLightSumColor * diffuseColor * DiffuseComponent + specularLightSumColor * specularIntensity;

		if ( raytracer.MustInterrupt ( totalColor, traceData ) )
			return	totalColor;
		
		bool willReflect;
		float reflectionIntensity;

		if ( entering ) {
			willReflect = ReflectionComponent > 0 && traceData.NumReflections < raytracer.MaxReflections;
			reflectionIntensity = ReflectionComponent;
		} else {
			willReflect = InnerReflectionComponent > 0 && traceData.NumInnerReflections < raytracer.MaxInnerReflections;
			reflectionIntensity = InnerReflectionComponent;
		}

		if ( willReflect && ReflectionMap != null && ReflectionMapInfluence > 0 ) {
			var reflectionMapRt = TextureCache.FromUnityTexture ( ReflectionMap );
			Color reflectionMapColor = reflectionMapRt.GetFilteredPixel ( texCoord.x, texCoord.y );
			float reflectionMapIntensity = reflectionMapColor.grayscale * reflectionMapColor.a;

			float reflectionMapInfluence = Mathf.Clamp01 ( ReflectionMapInfluence );
			reflectionIntensity = Mathf.Lerp ( reflectionIntensity, reflectionMapIntensity * reflectionIntensity, reflectionMapInfluence );

			willReflect = reflectionIntensity > 0;
		}

		float refractionIntensity = RefractionComponent;

		if ( RefractWhereTranslucent )
			refractionIntensity *= 1 - diffuseColor.a * DiffuseComponent;
		
		bool willRefract = refractionIntensity > 0 && traceData.NumRefractions < raytracer.MaxRefractions;
		bool forkingRequired = willReflect && willRefract;
		TraceData tdForRefraction;

		if ( forkingRequired )
			tdForRefraction = traceData.Fork ();
		else
			tdForRefraction = traceData;

		if ( willReflect ) {
			if ( entering ) {
				traceData.NumReflections++;
				traceData.Counters.Reflections++;
			} else {
				traceData.NumInnerReflections++;
				traceData.Counters.InnerReflections++;
			}

			Vector3 reflectionDir = Vector3.Reflect ( ray.direction, surfaceNormal );
			Vector3 pushedOutPoint = hit.point + reflectionDir * Raytracer.PushOutMagnitude;
			Color reflectionColor = raytracer.Trace ( new Ray ( pushedOutPoint, reflectionDir ), traceData );
			totalColor += reflectionColor * reflectionIntensity;

			if ( raytracer.MustInterrupt ( totalColor, traceData ) )
				return	totalColor;
		}

		if ( willRefract ) {
			tdForRefraction.NumRefractions++;
			tdForRefraction.Counters.Refractions++;
			CoefficientOut = RefractionIndex;
			CoefficientIn = 1 / RefractionIndex;

			if ( CoefficientOut > 1 ) {
				CriticalOutAngleCos = Mathf.Sqrt ( 1 - CoefficientIn * CoefficientIn );
				CriticalInAngleCos = 0;
			} else {
				CriticalOutAngleCos = 0;
				CriticalInAngleCos = Mathf.Sqrt ( 1 - CoefficientOut * CoefficientOut );
			}

			float criticalAngleCos = entering ? CriticalInAngleCos : CriticalOutAngleCos;
			Vector3 refractionDir;
			float nDotRay = Vector3.Dot ( surfaceNormal, ray.direction );

			if ( Mathf.Abs ( nDotRay ) >= criticalAngleCos ) {
				if ( entering )
					tdForRefraction.PenetrationStack.Push ( hit );
				else
					tdForRefraction.PenetrationStack.Pop ();

				float k = entering ? CoefficientIn : CoefficientOut;
				refractionDir = Raytracer.Refract ( ray.direction, surfaceNormal, nDotRay, k );
				refractionDir.Normalize ();
			} else	// Total internal reflection.
				refractionDir = Vector3.Reflect ( ray.direction, surfaceNormal );

			Vector3 pushedOutPoint = hit.point + refractionDir * Raytracer.PushOutMagnitude;
			Color refractionColor = raytracer.Trace ( new Ray ( pushedOutPoint, refractionDir ), tdForRefraction );

			if ( ColorAberration != 0 && entering ) {
				//float rDotRay = Vector3.Dot ( refractionDir, ray.direction );
				refractionColor = HsvColor.ChangeHue ( refractionColor, ( 1 + nDotRay ) * ColorAberration );
			}

			totalColor += refractionColor * refractionIntensity;

			if ( raytracer.MustInterrupt ( totalColor, tdForRefraction ) )
				return	totalColor;
		}

		return	totalColor;
	}
}
