// overworld_hex_tile.gdshader
// 大地图六边形瓦片 shader — MultiMeshInstance2D (canvas_item)
// 方案：Flow Map 驱动纹理方向 + 六边形SDF裁剪 + 边缘柔化
// 纯色模式下仍做 SDF 裁剪和边缘柔化（让六边形边界更自然）

shader_type canvas_item;

// --- 地形纹理（可选）---
uniform sampler2D terrain_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool use_texture = false;
uniform float texture_scale : hint_range(0.1, 4.0) = 1.0;

// --- Flow Map ---
uniform float flow_scale : hint_range(0.0001, 0.01) = 0.002;  // 流向空间频率
uniform float flow_strength : hint_range(0.0, 1.0) = 0.6;     // 流向旋转强度

// --- 六边形裁剪 ---
uniform float hex_edge_softness : hint_range(0.0, 0.08) = 0.03;
uniform float edge_darken_strength : hint_range(0.0, 0.3) = 0.10;

varying vec2 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

// ============================================================
// 工具函数
// ============================================================

// 程序化流向场（大尺度风向/地势方向）
vec2 procedural_flow(vec2 wp) {
    vec2 p = wp * flow_scale;
    float angle = sin(p.x * 0.6 + p.y * 0.4) * 3.14159
                + cos(p.x * 0.3 - p.y * 0.7) * 1.2;
    return vec2(cos(angle), sin(angle));
}

// 2D 旋转
mat2 rot2d(float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return mat2(vec2(c, -s), vec2(s, c));
}

// 平顶六边形 SDF（UV空间，中心0.5,0.5）
float hex_sdf_uv(vec2 uv) {
    vec2 p = abs(uv - 0.5) * 2.0;
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

// ============================================================
// Fragment
// ============================================================

void fragment() {
    // --- 六边形 SDF 裁剪 ---
    float sdf = hex_sdf_uv(UV);
    if (sdf > hex_edge_softness) {
        discard;
    }
    float edge_alpha = 1.0 - smoothstep(0.0, hex_edge_softness, sdf);

    vec4 col;

    if (use_texture) {
        // --- Flow Map 驱动纹理方向 ---
        vec2 flow_dir = procedural_flow(world_pos);
        float flow_angle = atan(flow_dir.y, flow_dir.x) * flow_strength;

        // 世界坐标连续 UV
        vec2 base_uv = world_pos * texture_scale * 0.005;

        // 以局部区域中心为锚点旋转
        float hex_size = 156.0; // HexOverworldTile.HexSize
        vec2 pivot = floor(world_pos / (hex_size * 2.0)) * (hex_size * 2.0) + hex_size;
        vec2 offset = world_pos - pivot;
        vec2 rotated = rot2d(flow_angle) * offset;
        vec2 final_uv = (pivot + rotated) * texture_scale * 0.005;

        col = texture(terrain_texture, final_uv);
    } else {
        // --- 纯色模式 ---
        col = COLOR;
    }

    // --- 边缘柔化 ---
    float ef = smoothstep(-0.12, -0.01, sdf);
    col.rgb *= mix(1.0, 1.0 - edge_darken_strength, ef);

    col.a *= edge_alpha;
    COLOR = col;
}
