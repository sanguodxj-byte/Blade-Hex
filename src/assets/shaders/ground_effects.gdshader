shader_type canvas_item;

// 地面环境特效 — 积水 + 噪声纹理 + 雪覆盖
// 方案参考：
//   - godotshaders.com "2D Water Puddles" — 反射 + 涟漪
//   - godotshaders.com "Procedural Noise Terrain" — 程序化地面纹理
//
// 使用方式：
//   1. 给地面覆盖层（ColorRect / Sprite2D）挂 ShaderMaterial
//   2. 赋此着色器
//   3. 通过 uniform 控制积水/噪声/雪覆盖强度
//   4. C# 端根据天气状态动态调整 uniform

group_uniforms GroundMode;
uniform int ground_mode = 0; // 0 = 积水(雨天), 1 = 沙地噪声, 2 = 雪覆盖
uniform float effect_intensity : hint_range(0.0, 1.0) = 0.0; // 效果强度（渐入渐出）

// === 积水参数 ===
group_uniforms Puddles;
uniform float puddle_coverage : hint_range(0.0, 1.0) = 0.4;  // 积水覆盖率
uniform float puddle_scale : hint_range(0.5, 8.0) = 3.0;     // 积水纹理缩放
uniform vec4 puddle_color : source_color = vec4(0.3, 0.4, 0.55, 0.6); // 积水颜色
uniform float ripple_speed : hint_range(0.5, 5.0) = 2.0;     // 涟漪速度
uniform float ripple_strength : hint_range(0.0, 0.5) = 0.15; // 涟漪强度
uniform float puddle_reflection : hint_range(0.0, 1.0) = 0.3; // 反射强度

// === 地面噪声纹理参数 ===
group_uniforms GroundNoise;
uniform float noise_scale : hint_range(1.0, 20.0) = 6.0;     // 噪声缩放
uniform vec4 noise_color_a : source_color = vec4(0.75, 0.65, 0.45, 1.0); // 沙地色A
uniform vec4 noise_color_b : source_color = vec4(0.6, 0.5, 0.35, 1.0);  // 沙地色B
uniform float noise_detail : hint_range(1.0, 8.0) = 4.0;     // 噪声细节层数
uniform float sand_drift_speed : hint_range(0.0, 2.0) = 0.3; // 沙地漂移速度

// === 雪覆盖参数 ===
group_uniforms SnowCover;
uniform float snow_coverage : hint_range(0.0, 1.0) = 0.6;    // 雪覆盖率
uniform float snow_scale : hint_range(1.0, 10.0) = 4.0;      // 雪纹理缩放
uniform vec4 snow_tint : source_color = vec4(0.92, 0.95, 1.0, 0.8); // 雪色调
uniform float snow_sparkle : hint_range(0.0, 1.0) = 0.3;     // 雪面闪烁

// === 世界坐标 ===
group_uniforms World;
uniform vec2 camera_offset = vec2(0.0, 0.0); // 摄像机偏移（让纹理跟随世界）

varying vec2 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

// ========================================
// 噪声工具函数
// ========================================

float hash21(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

vec2 hash22(vec2 p) {
	p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
	return fract(sin(p) * 43758.5453);
}

float noise2d(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f); // smoothstep
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// 分形噪声
float fbm(vec2 p, int octaves) {
	float value = 0.0;
	float amplitude = 0.5;
	float frequency = 1.0;
	for (int i = 0; i < octaves; i++) {
		value += amplitude * noise2d(p * frequency);
		frequency *= 2.0;
		amplitude *= 0.5;
	}
	return value;
}

// Voronoi 噪声（用于积水形状）
float voronoi(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float min_dist = 1.0;
	for (int x = -1; x <= 1; x++) {
		for (int y = -1; y <= 1; y++) {
			vec2 neighbor = vec2(float(x), float(y));
			vec2 point = hash22(i + neighbor);
			vec2 diff = neighbor + point - f;
			float dist = length(diff);
			min_dist = min(min_dist, dist);
		}
	}
	return min_dist;
}

// ========================================
// 积水涟漪
// ========================================

float ripple(vec2 p, float time) {
	// 多个涟漪源叠加
	float result = 0.0;
	for (int i = 0; i < 5; i++) {
		vec2 center = hash22(vec2(float(i) * 7.13, float(i) * 3.77));
		float phase = hash21(vec2(float(i), 0.0)) * 6.28;
		float dist = length(p - center);
		// 同心圆涟漪
		float wave = sin(dist * 30.0 - time * ripple_speed + phase) * 0.5 + 0.5;
		// 衰减
		wave *= smoothstep(0.4, 0.0, dist);
		result += wave;
	}
	return result * ripple_strength;
}

void fragment() {
	float time = TIME;
	vec2 uv = UV;
	// 世界空间 UV（让纹理不随摄像机滑动）
	vec2 world_uv = (world_pos + camera_offset) * 0.001;

	vec4 bg = texture(TEXTURE, UV);

	// ===== 积水模式（雨天）=====
	if (ground_mode == 0) {
		// Voronoi 决定积水区域
		float v = voronoi(world_uv * puddle_scale);
		float puddle_mask = smoothstep(puddle_coverage, puddle_coverage * 0.5, v);

		// 涟漪效果（仅在积水区域内）
		float rip = ripple(fract(world_uv * puddle_scale), time) * puddle_mask;

		// 积水颜色 + 涟漪扰动
		vec4 water = puddle_color;
		water.rgb += vec3(rip * 0.3); // 涟漪高光

		// 简易反射（翻转 UV 采样模拟）
		vec2 reflect_uv = UV + vec2(rip * 0.01, -rip * 0.02);
		vec4 reflected = texture(TEXTURE, reflect_uv);
		water.rgb = mix(water.rgb, reflected.rgb * 0.7, puddle_reflection);

		// 混合到背景
		COLOR = mix(bg, water, puddle_mask * effect_intensity);
	}

	// ===== 沙地噪声模式（沙尘暴）=====
	else if (ground_mode == 1) {
		// 多层 FBM 噪声生成沙地纹理
		vec2 drift = vec2(time * sand_drift_speed * 0.1, 0.0);
		float n = fbm((world_uv + drift) * noise_scale, int(noise_detail));

		// 双色混合
		vec4 sand = mix(noise_color_a, noise_color_b, n);

		// 细节层：更高频噪声叠加
		float detail = noise2d((world_uv + drift * 0.5) * noise_scale * 4.0);
		sand.rgb += (detail - 0.5) * 0.1;

		// 混合到背景
		COLOR = mix(bg, sand, effect_intensity * 0.5);
	}

	// ===== 雪覆盖模式 =====
	else if (ground_mode == 2) {
		// Voronoi + FBM 决定雪覆盖区域（模拟不均匀积雪）
		float v = voronoi(world_uv * snow_scale);
		float n = fbm(world_uv * snow_scale * 2.0, 3);
		float snow_mask = smoothstep(1.0 - snow_coverage, 1.0 - snow_coverage * 0.3, 1.0 - v * 0.6 - n * 0.4);

		// 雪面闪烁（高频噪声模拟冰晶反光）
		float sparkle = noise2d(world_uv * 50.0 + time * 0.5);
		sparkle = pow(sparkle, 8.0) * snow_sparkle;

		vec4 snow = snow_tint;
		snow.rgb += vec3(sparkle * 0.4);

		// 混合到背景
		COLOR = mix(bg, snow, snow_mask * effect_intensity);
	}

	else {
		COLOR = bg;
	}
}
