shader_type canvas_item;
render_mode blend_add;

// ========================================
// 程序化闪电 Shader — 局部闪电光弧 + 光晕
// 用于主菜单背景氛围效果
// 技术：噪声扭曲的线段 + 径向光晕 + 分支
// ========================================

uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float bolt_intensity : hint_range(0.0, 3.0) = 1.5;
uniform float bolt_width : hint_range(0.001, 0.05) = 0.012;
uniform float glow_radius : hint_range(0.0, 0.5) = 0.15;
uniform float glow_intensity : hint_range(0.0, 2.0) = 0.6;
uniform vec2 bolt_start = vec2(0.3, 0.0);
uniform vec2 bolt_end = vec2(0.5, 1.0);
uniform vec3 bolt_color : source_color = vec3(0.7, 0.8, 1.0);
uniform float branch_probability : hint_range(0.0, 1.0) = 0.4;
uniform float fade : hint_range(0.0, 1.0) = 1.0;

// 噪声函数
float hash(float n) {
    return fract(sin(n) * 43758.5453);
}

float noise(float x) {
    float i = floor(x);
    float f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(hash(i), hash(i + 1.0), f);
}

float fbm(float x) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; i++) {
        v += a * noise(x);
        x *= 2.3;
        a *= 0.5;
    }
    return v;
}

// 计算点到线段的距离（带噪声扭曲）
float bolt_distance(vec2 p, vec2 a, vec2 b, float seed) {
    vec2 pa = p - a;
    vec2 ba = b - a;
    float t = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    
    // 沿线段方向的噪声偏移（产生锯齿形闪电）
    float noise_freq = 12.0;
    float displacement = fbm(t * noise_freq + seed + time_offset * 20.0) - 0.5;
    displacement *= 0.08 * (1.0 - pow(abs(t - 0.5) * 2.0, 2.0)); // 两端收窄
    
    // 垂直于线段方向偏移
    vec2 perp = normalize(vec2(-ba.y, ba.x));
    vec2 closest = a + ba * t + perp * displacement;
    
    return length(p - closest);
}

// 单条闪电光弧
float lightning_bolt(vec2 uv, vec2 start, vec2 end, float width, float seed) {
    float d = bolt_distance(uv, start, end, seed);
    
    // 核心亮线（锐利）
    float core = exp(-d * d / (width * width * 0.5));
    
    // 外层辉光（柔和）
    float glow = exp(-d * d / (width * width * 8.0)) * 0.4;
    
    return core + glow;
}

void fragment() {
    vec2 uv = UV;
    float total = 0.0;
    
    // 主闪电
    total += lightning_bolt(uv, bolt_start, bolt_end, bolt_width, 0.0);
    
    // 分支闪电（从主干分叉出去）
    if (branch_probability > 0.1) {
        // 分支 1
        float t1 = 0.3 + hash(time_offset * 7.0) * 0.3;
        vec2 branch_origin = mix(bolt_start, bolt_end, t1);
        vec2 branch_dir = normalize(bolt_end - bolt_start);
        vec2 branch_perp = vec2(-branch_dir.y, branch_dir.x);
        float side1 = sign(hash(time_offset * 3.0) - 0.5);
        vec2 branch_end1 = branch_origin + branch_dir * 0.2 + branch_perp * side1 * 0.12;
        total += lightning_bolt(uv, branch_origin, branch_end1, bolt_width * 0.6, 5.0) * branch_probability;
        
        // 分支 2
        float t2 = 0.5 + hash(time_offset * 11.0) * 0.3;
        vec2 branch_origin2 = mix(bolt_start, bolt_end, t2);
        float side2 = -side1;
        vec2 branch_end2 = branch_origin2 + branch_dir * 0.15 + branch_perp * side2 * 0.1;
        total += lightning_bolt(uv, branch_origin2, branch_end2, bolt_width * 0.5, 11.0) * branch_probability * 0.7;
    }
    
    // 局部光晕（闪电周围的环境照亮）
    vec2 bolt_center = mix(bolt_start, bolt_end, 0.4);
    float center_dist = length(uv - bolt_center);
    float ambient_glow = exp(-center_dist * center_dist / (glow_radius * glow_radius)) * glow_intensity;
    
    // 合成
    float brightness = (total * bolt_intensity + ambient_glow) * fade;
    
    COLOR = vec4(bolt_color * brightness, brightness);
}
