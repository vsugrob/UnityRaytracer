using UnityEngine;
using System.Collections;

public enum AttenuationKind {
	Logarithmic,
	Quadratic,
	Linear
}

public class Diffuse : RaytracerMaterial {
	public Color Color = Color.gray;
	public AttenuationKind Attenuation = AttenuationKind.Quadratic;
	private float LightIntensityFactor = 2f;
	private float DiffuseExponent = 1.5f;

	public override Color GetColor ( Raytracer raytracer, Ray ray, RaycastHit hit, TraceData traceData ) {
		Color lightSumColor = RenderSettings.ambientLight;
		var lights = Light.GetLights ( LightType.Point, 0 );

		foreach ( var light in lights ) {
			Vector3 vToLight = light.transform.position - hit.point;
			float distance = vToLight.magnitude;

			if ( distance >= light.range )
				continue;

			float lightVolumeRadius = distance * 0.2f;
			distance = Mathf.Abs ( distance - lightVolumeRadius );
			Vector3 dir = vToLight.normalized;
			float diffuseIntensity = Vector3.Dot ( dir, hit.normal );

			if ( diffuseIntensity <= 0 )
				continue;

			diffuseIntensity = Mathf.Pow ( diffuseIntensity, DiffuseExponent );

			float attenuation;

			switch ( Attenuation ) {
			case AttenuationKind.Logarithmic:
				const float AttenuationBase = 2.7f;
				attenuation = 1.0f / Mathf.Log ( distance + AttenuationBase, AttenuationBase );
				break;
			case AttenuationKind.Quadratic:
				attenuation = 1 - distance / light.range;
				attenuation = attenuation * attenuation;
				break;
			case AttenuationKind.Linear:
			default:
				attenuation = 1 - distance / light.range;
				break;
			}

			Color lightColor = light.color * attenuation * diffuseIntensity * light.intensity * LightIntensityFactor;
			lightSumColor += lightColor;
		}

		Color totalColor = lightSumColor * this.Color;

		return	totalColor;
	}
}
