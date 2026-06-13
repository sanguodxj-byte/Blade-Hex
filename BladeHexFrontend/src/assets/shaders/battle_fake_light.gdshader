shader_type spatial;
render_mode unshaded, cull_disabled, blend_add;

uniform vec4 light_color : source_color = vec4(1.0, 0.72, 0.28, 0.85);
uniform float intensity : hint_range(0.0, 4.0) = 1.0;
uniform float falloff_power : hint_range(0.5, 5.0) = 2.2;
uniform float ring_strength : hint_range(0.0, 2.0) = 0.0;
uniform float ring_radius : hint_range(0.05, 0.95) = 0.48;
uniform float ring_width : hint_range(0.01, 0.30) = 0.08;

float radial_alpha(vec2 uv) {
	vec2 p = uv * 2.0 - vec2(1.0);
	float dist = length(p);
	float edge = 1.0 - smoothstep(0.92, 1.0, dist);
	float core = pow(max(1.0 - dist, 0.0), falloff_power);
	float ring_dist = abs(dist - ring_radius);
	float ring = (1.0 - smoothstep(ring_width, ring_width * 1.8, ring_dist)) * ring_strength;
	return clamp((core + ring) * edge, 0.0, 1.0);
}

void fragment() {
	float alpha = radial_alpha(UV) * light_color.a * intensity;
	ALBEDO = light_color.rgb * intensity;
	EMISSION = light_color.rgb * intensity;
	ALPHA = clamp(alpha, 0.0, 1.0);
}
