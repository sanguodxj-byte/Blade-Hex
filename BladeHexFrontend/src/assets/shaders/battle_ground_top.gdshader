// battle_ground_top.gdshader
// HD-2D 战斗地面顶面 shader
// 方案：Stochastic Tiling 消除平铺重复 + Flow Map 驱动纹理方向 + 六边形SDF裁剪 + 高亮
//
// ★注意★：多数地形下，棱柱顶面会被 GrassOverlayBatcher 的 2D 平铺精灵层完全覆盖，
//   该精灵层才是玩家实际看到的地表。改本 shader 的顶面着色对可见地表通常不可见，
//   要调地表受光请改 GrassOverlayBatcher。本 shader 仍负责：侧面(cliff 见 battle_ground_cliff),
//   无覆盖精灵的地形顶面、以及高亮叠色。
//
// Per-instance custom data:
//   R = highlight_flag (0/1), G = hl_color.R, B = hl_color.G, A = hl_color.B

shader_type spatial;
render_mode cull_back, depth_draw_opaque;

// --- 顶面主贴图 ---
uniform sampler2D top_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform float texture_tile_scale : hint_range(0.02, 2.0) = 0.12;

// --- Stochastic Tiling 参数 ---
uniform float blend_sharpness : hint_range(1.0, 20.0) = 8.0;
uniform float color_variance : hint_range(0.0, 0.15) = 0.03;
uniform bool use_stochastic = true;
uniform bool use_stochastic_rotation = true;

// --- Flow Map（可选外部提供，否则用程序化噪声生成）---
uniform sampler2D flow_map : filter_linear, repeat_enable;
uniform bool use_external_flow = false;
uniform float flow_scale : hint_range(0.001, 0.05) = 0.008;
uniform float flow_strength : hint_range(0.0, 1.0) = 0.7;
uniform bool use_flow_rotation = true;

// --- 六边形裁剪 ---
uniform float hex_radius = 96.0;
uniform float hex_edge_softness : hint_range(0.0, 0.1) = 0.025;
uniform float edge_darken : hint_range(0.0, 0.3) = 0.10;
uniform bool use_edge_darkening = true;

// --- 远景大气透视压灰 ---
uniform vec3 depth_tint_color = vec3(0.58, 0.65, 0.72);
uniform float depth_tint_strength : hint_range(0.0, 0.35) = 0.14;
uniform float depth_fade_start = 900.0;
uniform float depth_fade_end = 2400.0;

// --- 侧面着色(法线 Y < 0.5 时视为侧面) ---
uniform vec3 side_color = vec3(0.38, 0.30, 0.22);  // 默认泥土棕
uniform sampler2D side_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform bool use_side_texture = false;               // 是否使用侧面贴图
uniform float side_darken_bottom = 0.35;             // 底部 AO 暗化

varying vec3 v_world_pos;
varying vec3 v_local_pos;
varying vec3 v_normal_world;
varying float v_highlight_flag;
varying vec3 v_highlight_color;

void vertex() {
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_local_pos = VERTEX;
    v_normal_world = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    v_highlight_flag = INSTANCE_CUSTOM.r;
    v_highlight_color = INSTANCE_CUSTOM.gba;
}

// ============================================================
// 工具函数
// ============================================================

vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return fract(sin(p) * 43758.5453);
}

float hash1(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

mat2 rot2d(float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return mat2(vec2(c, s), vec2(-s, c));
}

// 程序化流向场
vec2 procedural_flow(vec2 world_xz) {
    vec2 p = world_xz * flow_scale;
    float angle = sin(p.x * 0.7 + p.y * 0.3) * 3.14159
                + cos(p.x * 0.2 - p.y * 0.8) * 1.5;
    return vec2(cos(angle), sin(angle));
}

// 平顶六边形 SDF
float hex_sdf(vec2 p) {
    p = abs(p);
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

// ============================================================
// Stochastic Tiling — Voronoi 三采样混合
// ============================================================

vec3 stochastic_sample(vec2 uv) {
    vec2 cell = floor(uv);
    vec2 frac_uv = fract(uv);

    float d0 = 100.0, d1 = 100.0, d2 = 100.0;
    vec2 seed0, seed1, seed2;

    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 neighbor = vec2(float(x), float(y));
            vec2 point = hash2(cell + neighbor);
            vec2 diff = neighbor + point - frac_uv;
            float dist = dot(diff, diff);

            if (dist < d0) {
                d2 = d1; seed2 = seed1;
                d1 = d0; seed1 = seed0;
                d0 = dist; seed0 = cell + neighbor + point;
            } else if (dist < d1) {
                d2 = d1; seed2 = seed1;
                d1 = dist; seed1 = cell + neighbor + point;
            } else if (dist < d2) {
                d2 = dist; seed2 = seed1;
            }
        }
    }

    // 每个种子的随机旋转+偏移
    vec2 uv_offset0 = hash2(seed0 + vec2(13.0, 7.0));
    vec2 uv_offset1 = hash2(seed1 + vec2(13.0, 7.0));
    vec2 uv_offset2 = hash2(seed2 + vec2(13.0, 7.0));

    vec3 col0, col1, col2;
    if (use_stochastic_rotation) {
        float angle0 = hash1(seed0) * 6.28318;
        float angle1 = hash1(seed1 + vec2(7.3, 2.1)) * 6.28318;
        float angle2 = hash1(seed2 + vec2(3.7, 9.2)) * 6.28318;
        col0 = texture(top_texture, rot2d(angle0) * uv + uv_offset0).rgb;
        col1 = texture(top_texture, rot2d(angle1) * uv + uv_offset1).rgb;
        col2 = texture(top_texture, rot2d(angle2) * uv + uv_offset2).rgb;
    } else {
        col0 = texture(top_texture, uv + uv_offset0).rgb;
        col1 = texture(top_texture, uv + uv_offset1).rgb;
        col2 = texture(top_texture, uv + uv_offset2).rgb;
    }

    // 微小色调偏移
    if (color_variance > 0.001) {
        float hue0 = (hash1(seed0 + vec2(5.5, 3.3)) - 0.5) * color_variance;
        float hue1 = (hash1(seed1 + vec2(5.5, 3.3)) - 0.5) * color_variance;
        float hue2 = (hash1(seed2 + vec2(5.5, 3.3)) - 0.5) * color_variance;
        col0 += vec3(hue0, hue0 * 0.5, -hue0 * 0.3);
        col1 += vec3(hue1, hue1 * 0.5, -hue1 * 0.3);
        col2 += vec3(hue2, hue2 * 0.5, -hue2 * 0.3);
    }

    // 距离权重
    float w0 = exp(-d0 * blend_sharpness);
    float w1 = exp(-d1 * blend_sharpness);
    float w2 = exp(-d2 * blend_sharpness);
    float wsum = w0 + w1 + w2;

    return (col0 * w0 + col1 * w1 + col2 * w2) / wsum;
}

// ============================================================
// Fragment
// ============================================================

void fragment() {
    // --- 六边形 SDF 裁剪 ---
    vec2 local_xz = v_local_pos.xz / hex_radius;
    float sdf = hex_sdf(local_xz);
    float edge_factor = 1.0 - smoothstep(-0.05, 0.0, sdf);

    // --- Flow Map 驱动纹理方向 ---
    vec2 flow_dir;
    if (use_external_flow) {
        vec2 flow_sample = texture(flow_map, v_world_pos.xz * 0.001).rg;
        flow_dir = (flow_sample - 0.5) * 2.0;
    } else {
        flow_dir = procedural_flow(v_world_pos.xz);
    }

    float flow_angle = atan(flow_dir.y, flow_dir.x) * flow_strength;

    // 基础 UV（世界坐标连续）
    vec2 base_uv = v_world_pos.xz * texture_tile_scale;

    // 以瓦片中心为锚点旋转 UV
    vec2 final_uv;
    if (use_flow_rotation && flow_strength > 0.001) {
        vec2 pivot = floor(v_world_pos.xz / (hex_radius * 2.0)) * (hex_radius * 2.0) + hex_radius;
        vec2 offset = v_world_pos.xz - pivot;
        vec2 rotated_offset = rot2d(flow_angle) * offset;
        final_uv = (pivot + rotated_offset) * texture_tile_scale;
    } else {
        final_uv = base_uv;
    }

    // --- 侧面 vs 顶面判定(法线 Y < 0.5 时视为侧面) ---
    bool is_side = v_normal_world.y < 0.5;

    vec3 col;
    if (is_side) {
        // 侧面:用 side_texture 或 side_color
        vec2 side_uv = vec2(
            (v_world_pos.x + v_world_pos.z) * 0.00125,
            v_world_pos.y * 0.0025
        );

        if (use_side_texture) {
            // 使用独立侧面贴图
            col = texture(side_texture, side_uv).rgb;
        } else {
            // 回退：side_color + top_texture 混合
            vec3 side_tex = texture(top_texture, side_uv).rgb;
            col = mix(side_color, side_tex * side_color * 1.5, 0.3);
        }

        // 底部 AO:本地 Y 越低越暗
        float height_frac = clamp(v_local_pos.y / 48.0 + 0.5, 0.0, 1.0);
        col *= mix(1.0 - side_darken_bottom, 1.0, height_frac);

        // 受光交给 Godot 真实光照管线：写入真实世界法线（转换至 View 空间），不再烘焙假光
        NORMAL = (VIEW_MATRIX * vec4(v_normal_world, 0.0)).xyz;
    } else {
        // 顶面:原有逻辑
        if (use_stochastic) {
            col = stochastic_sample(final_uv);
        } else {
            col = texture(top_texture, final_uv).rgb;
        }
        if (use_edge_darkening) {
            col *= edge_factor;
        }

        // 受光交给 Godot 真实光照管线：写入向上法线（转换至 View 空间），不再烘焙假光/方向边影
        // 🌟 物理重构：赋予顶面完美的向上法线，确保地表完全、正确地接收太阳光与阴影
        NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
    }

    vec3 view_pos = (VIEW_MATRIX * vec4(v_world_pos, 1.0)).xyz;
    float view_depth = max(0.0, -view_pos.z);
    float depth_factor = smoothstep(depth_fade_start, depth_fade_end, view_depth);
    col = mix(col, depth_tint_color, depth_factor * depth_tint_strength);
    col *= 1.0 - depth_factor * 0.06;

    // --- 高亮 ---
    if (v_highlight_flag > 0.5) {
        col = mix(col, v_highlight_color, 0.35);
    }

    ALBEDO = col;
    ROUGHNESS = 0.92;
    SPECULAR = 0.05;
    METALLIC = 0.0;
}
