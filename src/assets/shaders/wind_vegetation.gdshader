shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled;

// ========================================
// 风力植被摇摆 Shader
// 适用于树木、灌木、草地等植被 Sprite3D/MeshInstance3D
// 读取全局风力参数，根据顶点高度产生摇摆动画
// ========================================

// 全局风力参数（由 WindSystem.cs 每帧更新）
global uniform vec2 wind_direction;
global uniform float wind_strength;
global uniform float wind_time;
global uniform float wind_gust;

// 本地参数
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap;
uniform float sway_amount : hint_range(0.0, 1.0) = 0.3;  // 摇摆幅度
uniform float sway_speed : hint_range(0.0, 5.0) = 1.5;   // 摇摆速度倍率
uniform float trunk_stiffness : hint_range(0.0, 1.0) = 0.7; // 树干刚性（越高底部越不动）
uniform float phase_offset : hint_range(0.0, 100.0) = 0.0;  // 相位偏移（让每棵树不同步）

void vertex() {
    // 顶点高度因子：越高的顶点摇摆越大（模拟树冠比树干摆动更多）
    // UV.y=0 是顶部，UV.y=1 是底部（Sprite3D 默认）
    float height_factor = 1.0 - UV.y;
    // 树干刚性：底部几乎不动
    height_factor = pow(height_factor, mix(1.0, 3.0, trunk_stiffness));
    
    // 风力时间 + 相位偏移（每棵树用不同 phase_offset 避免同步）
    float t = wind_time * sway_speed + phase_offset;
    
    // 主摇摆（低频正弦）
    float sway_main = sin(t * 1.2) * 0.6 + sin(t * 2.1 + 0.5) * 0.4;
    
    // 阵风叠加（高频抖动）
    float sway_gust = sin(t * 4.7 + phase_offset * 0.3) * wind_gust;
    
    // 合成摇摆量
    float total_sway = (sway_main + sway_gust) * sway_amount * wind_strength * height_factor;
    
    // 沿风向偏移顶点
    VERTEX.x += wind_direction.x * total_sway;
    VERTEX.z += wind_direction.y * total_sway;
    
    // 轻微垂直压缩（风大时树冠被压低）
    VERTEX.y -= abs(total_sway) * 0.1;
}

void fragment() {
    vec4 tex = texture(albedo_texture, UV);
    ALBEDO = tex.rgb;
    ALPHA = tex.a;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
}
