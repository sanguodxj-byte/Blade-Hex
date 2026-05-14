shader_type canvas_item;

// 天气粒子 — 雨/雪/沙尘暴（透明覆盖层）
// 循环上限使用编译期常量（Mobile 渲染器不支持 uniform 作为循环上限）

group_uniforms Weather;
uniform int weather_type = 0; // 0=雨, 1=雪, 2=沙尘暴
uniform float intensity : hint_range(0.0, 1.0) = 1.0;

group_uniforms Rain;
uniform float rain_slant : hint_range(-0.1, 0.1) = -0.01;
uniform float rain_speed : hint_range(25.0, 100.0) = 50.0;
uniform float rain_blur : hint_range(0.0005, 0.1) = 0.003;
uniform vec4 rain_color : source_color = vec4(0.7, 0.8, 1.0, 0.5);

group_uniforms Snow;
uniform float snow_speed : hint_range(0.1, 1.0) = 0.3;
uniform float snow_wobble : hint_range(0.0, 1.0) = 0.3;
uniform vec4 snow_color : source_color = vec4(1.0, 1.0, 1.0, 0.8);

group_uniforms Sandstorm;
uniform float sand_speed : hint_range(1.0, 20.0) = 8.0;
uniform float sand_vertical : hint_range(0.0, 5.0) = 1.5;
uniform vec4 sand_color : source_color = vec4(0.85, 0.72, 0.45, 0.6);
uniform float sand_fog_density : hint_range(0.0, 0.8) = 0.3;
uniform vec4 sand_fog_color : source_color = vec4(0.78, 0.65, 0.38, 0.4);

// 编译期常量循环上限
const int RAIN_COUNT = 60;
const int SNOW_COUNT = 80;
const int SAND_COUNT = 100;

float line_sdf(vec2 p, vec2 s) {
	vec2 d = abs(p) - s;
	return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

float hash11(float x) { return fract(sin(x * 18.34) * 51.78); }
float hash11b(float x) { return fract(sin(x * 25.42) * 21.24); }
float hash21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float noise2d(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float v = 0.0; float a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise2d(p); p *= 2.0; a *= 0.5; }
	return v;
}

void fragment() {
	float time = TIME + 1000.0;
	vec2 uv = UV;

	COLOR = vec4(0.0, 0.0, 0.0, 0.0);

	// ===== 雨 =====
	if (weather_type == 0) {
		vec2 s = vec2(0.0003, 0.015);
		vec2 rain_uv = uv;
		rain_uv.x += rain_uv.y * rain_slant;
		float output = 0.0;

		for (int i = 1; i <= RAIN_COUNT; i++) {
			float h1 = hash11(float(i));
			float h2 = hash11b(float(i));
			float pos_x = h1 * 1.2 + h1 * rain_uv.y * (-rain_slant);
			float pos_y = max(h2 * rain_speed, h1 * rain_speed);
			vec2 pos = vec2(pos_x, -mod(-pos_y * time * 0.1, -1.0));
			float sdf = line_sdf(rain_uv - pos, s);
			output += clamp(-sdf / rain_blur, 0.0, 1.0);
		}

		float alpha = min(output, 1.0) * intensity;
		COLOR = vec4(rain_color.rgb, rain_color.a * alpha);
	}

	// ===== 雪 =====
	else if (weather_type == 1) {
		float output = 0.0;

		for (int i = 1; i <= SNOW_COUNT; i++) {
			float h1 = hash11(float(i));
			float h2 = hash11b(float(i));
			float h3 = hash11(float(i) + 7.13);
			float sp = snow_speed * (0.5 + h2);
			float wobble_x = sin(time * sp * 2.0 + h1 * 6.28) * snow_wobble * h3;
			float px = fract(h1 + wobble_x * 0.1);
			float py = fract(-mod(-h2 * snow_speed * time * 0.5, -1.0));
			float dist = length(uv - vec2(px, py));
			output += smoothstep(0.006, 0.002, dist);
		}

		float alpha = min(output, 1.0) * intensity;
		COLOR = vec4(snow_color.rgb, snow_color.a * alpha);
	}

	// ===== 沙尘暴 =====
	else if (weather_type == 2) {
		vec2 fog_uv = uv * 3.0 + vec2(time * sand_speed * 0.05, time * sand_vertical * 0.02);
		float fog = fbm(fog_uv) * sand_fog_density;

		float particles = 0.0;
		for (int i = 1; i <= SAND_COUNT; i++) {
			float h1 = hash11(float(i));
			float h2 = hash11b(float(i));
			float h3 = hash11(float(i) + 3.77);
			float px = fract(h1 + time * sand_speed * 0.02 * (0.5 + h2));
			float vert_wobble = sin(time * sand_vertical + h3 * 6.28) * 0.02 * h2;
			float py = fract(h2 + vert_wobble);
			vec2 diff = uv - vec2(px, py);
			diff.x *= 0.4;
			float dist = length(diff);
			particles += smoothstep(0.003, 0.0006, dist);
		}

		float fog_alpha = fog * intensity * sand_fog_color.a;
		float particle_alpha = min(particles, 1.0) * intensity * sand_color.a * 0.7;
		float total_alpha = min(fog_alpha + particle_alpha, 1.0);
		vec3 col = mix(sand_fog_color.rgb, sand_color.rgb, particle_alpha / max(total_alpha, 0.001));
		COLOR = vec4(col, total_alpha);
	}
}
