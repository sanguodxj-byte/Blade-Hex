// overworld_hex_tile.gdshader
// 大地图六边形瓦片 shader — MultiMeshInstance2D (canvas_item)
// 纹理模式 & 纯色模式统一：SDF 裁剪 + 边缘羽化（alpha 渐变）
// 边缘羽化让不同地形之间柔和过渡，同类地形接缝也更不明显

shader_type canvas_item;

// --- 地形纹理（可选）---
uniform sampler2D terrain_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool use_texture = false;
uniform float texture_scale : hint_range(0.1, 8.0) = 1.0;

// --- 边缘羽化 ---
// feather_width: 羽化带宽度（UV 空间，0.0=硬边，0.15=很柔和）
uniform float feather_width : hint_range(0.0, 0.25) = 0.08;
// edge_darken: 边缘压暗强度（让边缘颜色稍暗，增加层次感）
uniform float edge_darken : hint_range(0.0, 0.4) = 0.06;

varying vec2 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

// 平顶六边形 SDF（UV空间，中心0.5,0.5，返回负值=内部，正值=外部）
float hex_sdf(vec2 uv) {
    vec2 p = abs(uv - 0.5) * 2.0;
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

void fragment() {
    float sdf = hex_sdf(UV);

    // 超出羽化带外缘 → 完全透明，丢弃
    if (sdf > feather_width) {
        discard;
    }

    // 羽化 alpha：从内部(1.0)到边缘(0.0)平滑过渡
    float alpha = 1.0 - smoothstep(0.0, feather_width, sdf);

    // 边缘压暗：越靠近边缘越暗
    float darken = 1.0 - edge_darken * smoothstep(-feather_width * 2.0, feather_width, sdf);

    vec4 col;

    if (use_texture) {
        // 世界坐标连续 UV — 同类地形无缝
        float uv_factor = texture_scale / 512.0;
        vec2 tex_uv = world_pos * uv_factor;
        col = texture(terrain_texture, tex_uv);
    } else {
        // 纯色模式
        col = COLOR;
    }

    col.rgb *= darken;
    col.a *= alpha;
    COLOR = col;
}
