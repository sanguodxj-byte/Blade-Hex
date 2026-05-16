// overworld_hex_3d.gdshader
// 3D 大地图六边形瓦片 shader — spatial
// 世界坐标连续 UV（消除接缝和重复感）+ 六边形边缘羽化

shader_type spatial;
render_mode cull_disabled;

// --- 地形纹理 ---
uniform sampler2D terrain_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform float texture_scale : hint_range(0.05, 4.0) = 0.5;
uniform bool use_texture = true;
uniform vec4 fallback_color : source_color = vec4(0.5, 0.55, 0.35, 1.0);

// --- 边缘羽化 ---
uniform float feather_width : hint_range(0.0, 0.3) = 0.08;

// --- 迷雾 ---
uniform float fog_amount : hint_range(0.0, 1.0) = 0.0; // 0=完全可见, 1=未探索
uniform vec4 fog_color : source_color = vec4(0.1, 0.12, 0.15, 1.0);

varying vec3 world_pos;
varying vec2 local_uv;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    local_uv = UV;
}

// 平顶六边形 SDF（UV 空间，中心 0.5,0.5）
float hex_sdf(vec2 uv) {
    vec2 p = abs(uv - 0.5) * 2.0;
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

void fragment() {
    // --- 六边形 SDF 裁剪 + 羽化 ---
    float sdf = hex_sdf(local_uv);
    if (sdf > feather_width) {
        discard;
    }
    float alpha = 1.0 - smoothstep(0.0, feather_width, sdf);

    // --- 世界坐标连续 UV（消除接缝）---
    vec3 color;
    if (use_texture) {
        vec2 tex_uv = world_pos.xz * texture_scale;
        color = texture(terrain_texture, tex_uv).rgb;
    } else {
        color = fallback_color.rgb;
    }

    // --- 迷雾混合 ---
    color = mix(color, fog_color.rgb, fog_amount);

    ALBEDO = color;
    ALPHA = alpha;
}
