shader_type canvas_item;

// 天气粒子 — 雨/雪 程序化着色
// 方案参考：
//   - godotshaders.com "Simple rain/snow shader" (mo32167) — SDF 雨滴
//   - godotshaders.com "Rain and Snow with Parallax" (Steampunkdemon) — 视差雨雪
//
// 使用方式：
//   1. 给 ColorRect / TextureRect（全屏覆盖层）挂 ShaderMaterial
//   2. 赋此着色器
//   3. 通过 uniform 切换天气类型，调参数

group_uniforms Weather;
uniform int weather_type = 0; // 0 = 雨, 1 = 雪

// === 雨参数（参考 mo32167 SDF 方案）===
group_uniforms Rain;
uniform int rain_count : hint_range(0, 2000) = 150;
uniform float rain_slant : hint_range(-0.1, 0.1) = -0.01;
uniform float rain_speed : hint_range(25.0, 100.0) = 50.0;
uniform float rain_blur : hint_range(0.0005, 0.1) = 0.002;
uniform vec4 rain_color : source_color = vec4(0.8, 0.85, 1.0, 1.0);
uniform vec2 rain_size = vec2(0.005, 0.2);

// === 雪参数（参考 Steampunkdemon 视差方案）===
group_uniforms Snow;
uniform int snow_count : hint_range(0, 1000) = 200;
uniform float snow_speed : hint_range(0.1, 1.0) = 0.3;
uniform float snow_size : hint_range(0.001, 0.02) = 0.008;
uniform float snow_wobble : hint_range(0.0, 1.0) = 0.3;
uniform vec4 snow_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// SDF 线段（mo32167 方案的核心）
float line_sdf(vec2 p, vec2 s) {
	vec2 d = abs(p) - s;
	return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

float hash11(float x) {
	return fract(sin(x * 18.34) * 51.78);
}

float hash11b(float x) {
	return fract(sin(x * 25.42) * 21.24);
}

void fragment() {
	float time = TIME + 1000.0;
	vec2 uv = UV;

	// ===== 雨模式 =====
	if (weather_type == 0) {
		vec2 s = rain_size * 0.1;
		uv.x += uv.y * rain_slant;
		float output = 0.0;

		for (int i = 1; i <= rain_count; i++) {
			float h1 = hash11(float(i));
			float h2 = hash11b(float(i));
			float sl = h1 * uv.y * (-rain_slant);
			float pos_x = h1 * 1.2 + sl;
			float pos_y = max(h2 * rain_speed, h1 * rain_speed);
			vec2 pos = vec2(pos_x, -mod(-pos_y * time * 0.1, -1.0));
			float sdf = line_sdf(uv - pos, s);
			output += clamp(-sdf / rain_blur, 0.0, 1.0);
		}

		vec4 bg = texture(TEXTURE, UV);
		COLOR = mix(bg, rain_color, min(output, 1.0));
	}

	// ===== 雪模式 =====
	if (weather_type == 1) {
		vec4 bg = texture(TEXTURE, UV);
		float output = 0.0;

		for (int i = 1; i <= snow_count; i++) {
			float h1 = hash11(float(i));
			float h2 = hash11b(float(i));
			float h3 = hash11(float(i) + 7.13);

			float sp = snow_speed * (0.5 + h2);
			float wobble_x = sin(time * sp * 2.0 + h1 * 6.28) * snow_wobble * h3;

			float px = fract(h1 + wobble_x * 0.1);
			float py = fract(-mod(-h2 * snow_speed * time * 0.5, -1.0));

			float dist = length(uv - vec2(px, py));
			output += smoothstep(snow_size, snow_size * 0.3, dist);
		}

		COLOR = mix(bg, snow_color, min(output, 1.0));
	}
}
