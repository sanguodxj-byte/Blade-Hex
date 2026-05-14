// battle_ground_top.gdshader
// HD-2D 战斗地面顶面 shader
// 方案：Flow Map 驱动纹理方向（草顺地势倒）+ 六边形SDF裁剪 + 假光 + 高亮
//
// Per-instance custom data:
//   R = highlight_flag (0/1), G = hl_color.R, B = hl_color.G, A = hl_color.B

shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

// --- 顶面主贴图 ---
uniform sampler2D top_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform float texture_tile_scale : hint_range(0.02, 2.0) = 0.12;

// --- Flow Map（可选外部提供，否则用程序化噪声生成）---
uniform sampler2D flow_map : filter_linear, repeat_enable;
uniform bool use_external_flow = false;
uniform float flow_scale : hint_range(0.001, 0.05) = 0.008; // 程序化流向的空间频率
uniform float flow_strength : hint_range(0.0, 1.0) = 0.7;   // 流向对UV旋转的影响强度

// --- 六边形裁剪 ---
uniform float hex_radius = 96.0;
uniform float hex_edge_softness : hint_range(0.0, 0.1) = 0.025;
uniform float edge_darken : hint_range(0.0, 0.3) = 0.10;

// --- 假光 ---
uniform vec3 fake_light_dir = vec3(0.35, 0.9, 0.25);
uniform float fake_light_min = 0.78;
uniform float fake_light_max = 1.05;

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

// 程序化流向场：用两层正弦波模拟大尺度风向/坡向
// 返回归一化方向向量（角度编码为 vec2）
vec2 procedural_flow(vec2 world_xz) {
    vec2 p = world_xz * flow_scale;
    // 主风向（大尺度，缓慢变化）
    float angle = sin(p.x * 0.7 + p.y * 0.3) * 3.14159
                + cos(p.x * 0.2 - p.y * 0.8) * 1.5;
    return vec2(cos(angle), sin(angle));
}

// 2D 旋转
mat2 rot2d(float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return mat2(vec2(c, -s), vec2(s, c));
}

// 平顶六边形 SDF
float hex_sdf(vec2 p) {
    p = abs(p);
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

// ============================================================
// Fragment
// ============================================================

void fragment() {
    // --- 六边形 SDF 裁剪 ---
    vec2 local_xz = v_local_pos.xz / hex_radius;
    float sdf = hex_sdf(local_xz);
    // 边缘柔化因子（内部=1，边缘渐暗，但不丢弃像素）
    float edge_factor = 1.0 - smoothstep(-0.05, 0.0, sdf);

    // --- Flow Map 驱动纹理方向 ---
    vec2 flow_dir;
    if (use_external_flow) {
        // 外部流向图：RG通道编码方向 (0.5,0.5)=无流向
        vec2 flow_sample = texture(flow_map, v_world_pos.xz * 0.001).rg;
        flow_dir = (flow_sample - 0.5) * 2.0;
    } else {
        flow_dir = procedural_flow(v_world_pos.xz);
    }

    // 从流向计算旋转角度
    float flow_angle = atan(flow_dir.y, flow_dir.x) * flow_strength;

    // 基础 UV（世界坐标连续）
    vec2 base_uv = v_world_pos.xz * texture_tile_scale;

    // 以瓦片中心为锚点旋转 UV
    vec2 pivot = floor(v_world_pos.xz / (hex_radius * 2.0)) * (hex_radius * 2.0) + hex_radius;
    vec2 offset = v_world_pos.xz - pivot;
    vec2 rotated_offset = rot2d(flow_angle) * offset;
    vec2 final_uv = (pivot + rotated_offset) * texture_tile_scale;

    vec3 col = texture(top_texture, final_uv).rgb;

    // --- 边缘柔化 ---
    col *= edge_factor;

    // --- 假光（只对顶面生效，避免棱边法线插值导致黑色顶点） ---
    // 顶面法线固定朝上，不使用插值法线
    vec3 top_normal = vec3(0.0, 1.0, 0.0);
    float ndl = clamp(dot(top_normal, normalize(fake_light_dir)), 0.0, 1.0);
    col *= mix(fake_light_min, fake_light_max, ndl);

    // --- 高亮 ---
    if (v_highlight_flag > 0.5) {
        col = mix(col, v_highlight_color, 0.35);
    }

    ALBEDO = col;
}
