shader_type canvas_item;

// ╔══════════════════════════════════════════════════════════════════╗
// ║ 强制要求：未探索区域必须是纯黑、完全不可见。                      ║
// ║ 任何修改此 shader 的人必须保证 visibility=0 时输出完全不透明黑色。 ║
// ║ 禁止让未探索区域透出任何地形颜色。                                ║
// ╚══════════════════════════════════════════════════════════════════╝

// 战争迷雾 — Sprite2D 覆盖层着色器
// 未探索区域完全不透明（黑色），已揭示区域完全透明。
// 未探索区域边缘有柔和羽化过渡。

group_uniforms Fog;
uniform vec4 fog_color : source_color = vec4(0.0, 0.0, 0.0, 1.0); // 迷雾颜色（纯黑）
uniform float edge_softness : hint_range(0.0, 0.5) = 0.15;         // 边缘羽化宽度（UV 空间）
uniform float fog_disabled = 0.0;                                    // 1.0 = 完全禁用迷雾
uniform vec2 fog_texture_size = vec2(64.0, 64.0);                    // 迷雾纹理尺寸（由代码设置）

void fragment() {
	// 从纹理读取可见度（R 通道）: 0=未探索（黑色）, 1=已揭示/视野内（透明）
	float visibility = texture(TEXTURE, UV).r;

	// 采样周围像素实现边缘羽化（3x3 高斯模糊近似）
	// 让黑色→透明的过渡像画笔边缘一样柔和
	vec2 texel = 1.0 / fog_texture_size;
	float blur = 0.0;
	blur += texture(TEXTURE, UV + vec2(-texel.x, -texel.y)).r * 0.0625;
	blur += texture(TEXTURE, UV + vec2( 0.0,     -texel.y)).r * 0.125;
	blur += texture(TEXTURE, UV + vec2( texel.x, -texel.y)).r * 0.0625;
	blur += texture(TEXTURE, UV + vec2(-texel.x,  0.0    )).r * 0.125;
	blur += texture(TEXTURE, UV                           ).r * 0.25;
	blur += texture(TEXTURE, UV + vec2( texel.x,  0.0    )).r * 0.125;
	blur += texture(TEXTURE, UV + vec2(-texel.x,  texel.y)).r * 0.0625;
	blur += texture(TEXTURE, UV + vec2( 0.0,      texel.y)).r * 0.125;
	blur += texture(TEXTURE, UV + vec2( texel.x,  texel.y)).r * 0.0625;

	// 柔和过渡：未探索(黑) → 已揭示(透明)，边缘像画笔抹开
	float alpha = 1.0 - smoothstep(0.0, edge_softness, blur);

	// 迷雾完全禁用时透明
	alpha = mix(alpha, 0.0, fog_disabled);

	COLOR = vec4(fog_color.rgb, alpha);
}
