shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix;

uniform vec4 shadow_color : source_color = vec4(0.03, 0.025, 0.018, 0.42);
uniform float softness : hint_range(0.05, 0.95) = 0.32;

float shadow_alpha(vec2 uv) {
	vec2 p = uv * 2.0 - vec2(1.0);
	p.x *= 0.82;
	p.y *= 1.18;
	float dist = length(p);
	return 1.0 - smoothstep(softness, 1.0, dist);
}

void fragment() {
	float alpha = shadow_alpha(UV) * shadow_color.a;
	ALBEDO = shadow_color.rgb;
	ALPHA = clamp(alpha, 0.0, shadow_color.a);
}
