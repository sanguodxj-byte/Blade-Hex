shader_type canvas_item;

// 战争迷雾柔化 — 给 FogOfWarRenderer._sprite 用的后处理着色器
//
// 输入: FogOfWarRenderer 生成的 ImageTexture（每格1像素，scale 到地图尺寸）
//   - R 通道 = 可见度阶梯（0=未探索, 0.3=已揭示, 1.0=视野内）
//   - 实际上输入是 RGBA 颜色，直接对 color 做模糊
//
// 作用:
//   1. 多采样模糊 — 让格子边界不再锐利方块
//   2. 时间扰动 — 迷雾边缘轻微飘动
//
// 使用: 在 FogOfWarRenderer.setup() 中给 _sprite 挂此材质

group_uniforms FogBlur;
uniform float blur_radius : hint_range(1.0, 8.0) = 3.0; // 模糊采样半径
uniform float time_distortion : hint_range(0.0, 0.02) = 0.005; // 时间扰动强度

// 9 点高斯模糊权重（简化版）
const float KERNEL[9] = {
	0.0625, 0.125, 0.0625,
	0.125,  0.25,  0.125,
	0.0625, 0.125, 0.0625
};

void fragment() {
	// 时间扰动
	vec2 time_offset = vec2(
		sin(TIME * 0.3 + UV.x * 10.0) * time_distortion,
		cos(TIME * 0.2 + UV.y * 10.0) * time_distortion
	);

	vec2 sample_uv = UV + time_offset;

	// 3×3 高斯模糊采样
	vec4 blurred = vec4(0.0);
	vec2 texel = TEXTURE_PIXEL_SIZE * blur_radius;

	int i = 0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 offset = vec2(float(x), float(y)) * texel;
			blurred += texture(TEXTURE, sample_uv + offset) * KERNEL[i];
			i++;
		}
	}

	COLOR = blurred;
}
