// battle_ground_cliff.gdshader
// HD-2D 战斗地图六棱柱侧面（悬崖）shader
//
// 用途：高程差 ≥ 1 时暴露的六棱柱侧壁
// 视觉：垂直 tileable，不随顶面 uv scale 变化；底部加暗。
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

uniform sampler2D cliff_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform float vertical_tile_scale : hint_range(0.01, 1.0) = 0.05;
uniform float horizontal_tile_scale : hint_range(0.01, 1.0) = 0.05;

uniform vec3 fake_light_dir = vec3(0.35, 0.9, 0.25);
uniform float fake_light_min = 0.55;
uniform float fake_light_max = 0.95;

uniform float base_ao_darken : hint_range(0.0, 1.0) = 0.35;

varying vec3 v_world_pos;
varying vec3 v_normal_world;
varying float v_height_frac;

void vertex() {
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_normal_world = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    // 本地 Y：范围 [-H/2, H/2]；底部 = 0，顶部 = 1
    v_height_frac = clamp(VERTEX.y / 48.0 + 0.5, 0.0, 1.0);
}

void fragment() {
    // 侧面 uv：用世界 Y 作为垂直坐标，水平用 XZ 投影距离
    float u = (v_world_pos.x + v_world_pos.z) * horizontal_tile_scale;
    float vv = v_world_pos.y * vertical_tile_scale;
    vec3 col = texture(cliff_texture, vec2(u, vv)).rgb;

    // 假光
    float ndl = clamp(dot(v_normal_world, normalize(fake_light_dir)), 0.0, 1.0);
    float light = mix(fake_light_min, fake_light_max, ndl);
    col *= light;

    // 底部变暗 AO
    col *= mix(1.0 - base_ao_darken, 1.0, v_height_frac);

    ALBEDO = col;
}
