shader_type canvas_item;

// 单位描边 — 选中高亮外扩描边
// 方案参考：GDQuest "outline2D_outer" (github.com/gdquest-demos/godot-shaders)
// 原理：8 方向采样 alpha，在纹理边缘外侧扩展描边
//
// 使用方式：
//   1. 给单位 Sprite / AnimatedSprite2D 挂 ShaderMaterial
//   2. 赋此着色器
//   3. 选中时设 show_outline = true，取消时设 false（通过 GDScript set_shader_parameter）

group_uniforms Outline;
uniform vec4 outline_color : source_color = vec4(1.0, 0.85, 0.0, 1.0); // 描边色（金黄）
uniform float line_thickness : hint_range(0.5, 10.0) = 2.0;           // 描边粗细
uniform bool show_outline = true;                                       // 是否显示描边

// GDQuest 方案：8 方向偏移（比 4 方向描边更均匀）
const vec2 OFFSETS[8] = {
	vec2(-1.0, -1.0), vec2(-1.0, 0.0), vec2(-1.0, 1.0), vec2(0.0, -1.0),
	vec2(0.0, 1.0), vec2(1.0, -1.0), vec2(1.0, 0.0), vec2(1.0, 1.0)
};

void fragment() {
	vec4 color = texture(TEXTURE, UV);

	if (!show_outline) {
		COLOR = color;
		return;
	}

	// 8 方向采样 alpha，累加得到描边掩码
	vec2 pixel_size = TEXTURE_PIXEL_SIZE * line_thickness;
	float outline = 0.0;

	for (int i = 0; i < 8; i++) {
		outline += texture(TEXTURE, UV + pixel_size * OFFSETS[i]).a;
	}
	outline = min(outline, 1.0);

	// 关键公式：outline - color.a
	//   纹理内部：outline ≈ 1.0, color.a ≈ 1.0 → mix factor ≈ 0（不画描边）
	//   纹理外部紧邻边缘：outline > 0, color.a ≈ 0 → mix factor > 0（画描边）
	float mix_factor = clamp(outline - color.a, 0.0, 1.0);

	COLOR = mix(color, outline_color, mix_factor);
}
