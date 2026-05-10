shader_type canvas_item;

// 地形边缘混合 — 平顶六边形 SDF 边缘 alpha 渐变
// 用于: HexOverworldRenderer 中每个 tile Sprite2D 的共享材质
//
// 原理:
//   纹理是 313×313 正方形，但六边形只有内部有内容。
//   用平顶六边形 SDF 在边缘生成 alpha 渐变带，
//   让相邻瓦片的边缘柔化混合而非硬切。
//
// 关键参数:
//   HEX_SIZE = 156 → 纹理 313px 以 1:1 显示
//   平顶六边形 SDF: 基于 q,r,s 三轴约束

group_uniforms EdgeBlend;
// 渐变带宽度（UV 空间 0~1，0.03 ≈ 10px @313tex）
uniform float blend_width : hint_range(0.0, 0.15) = 0.05;
// 1.0 = 完全基于 SDF，0.0 = 无效果
uniform float blend_strength : hint_range(0.0, 1.0) = 0.8;

// 平顶六边形 SDF
// 参数: p = 像素坐标（以六边形中心为原点），size = 外接圆半径
float hex_sdf_flat_top(vec2 p, float size) {
	// 平顶六边形的三个对称轴约束
	vec2 s = vec2(size, size * 1.7320508); // size, size*√3
	vec2 abs_p = abs(p);

	// 三个半平面距离
	float d1 = abs_p.x - s.x;                          // 左右边界
	float d2 = abs(s.x * 0.5 + s.y * 0.5) - (s.x * 0.5 * abs_p.x + s.y * 0.5 * abs_p.y); // 对角线

	// 合并
	float outside = max(d1, -d2);

	// 转为归一化的距离（正值=内部，负值=外部）
	return -outside;
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);

	// 纹理坐标转为中心原点像素坐标
	// TEXTURE_PIXEL_SIZE = 1/313, 所以 UV * 313 = 像素
	vec2 pixel_pos = (UV - 0.5) / TEXTURE_PIXEL_SIZE;

	// 六边形半径: 纹理显示范围内六边形的有效半径
	// 313px 纹理, HEX_SIZE=156 → 六边形约 156px 半径
	float hex_radius = 155.0;

	float dist = hex_sdf_flat_top(pixel_pos, hex_radius);

	// SDF > blend_zone: 完全可见
	// SDF < 0: 完全透明（六边形外部）
	// 中间: 渐变
	float blend_zone = blend_width * 313.0 * 0.5; // 转为像素单位

	float alpha_mult = 1.0;
	if (blend_strength > 0.0) {
		alpha_mult = smoothstep(0.0, blend_zone, dist);
		alpha_mult = mix(1.0, alpha_mult, blend_strength);
	}

	COLOR = vec4(tex.rgb, tex.a * alpha_mult);
}
