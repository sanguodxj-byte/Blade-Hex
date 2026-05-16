// overworld_hex_procedural.gdshader
// 程序化六边形地形 shader — 使用 simplex-like 噪声 + 域扭曲，避免网格化痕迹。

shader_type spatial;
render_mode cull_disabled;

// ─── 调色板 ───
uniform vec4 color_base : source_color = vec4(0.55, 0.45, 0.30, 1.0);
uniform vec4 color_dark : source_color = vec4(0.30, 0.22, 0.15, 1.0);
uniform vec4 color_light : source_color = vec4(0.75, 0.65, 0.45, 1.0);

// ─── 噪声参数 ───
uniform float noise_scale : hint_range(0.05, 4.0) = 0.6;
uniform float warp_strength : hint_range(0.0, 2.0) = 0.8; // 域扭曲强度

// ─── 墨线斑块 ───
uniform float ink_amount : hint_range(0.0, 1.0) = 0.30;
uniform float ink_threshold : hint_range(0.0, 1.0) = 0.42;

// ─── 六边形裁切 ───
uniform float feather_width : hint_range(0.0, 0.3) = 0.08;

// ─── 迷雾 ───
uniform float fog_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 fog_color : source_color = vec4(0.1, 0.12, 0.15, 1.0);

varying vec3 world_pos;
varying vec2 local_uv;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    local_uv = UV;
}

// ─── Simplex-like noise（无网格痕迹） ───
vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return -1.0 + 2.0 * fract(sin(p) * 43758.5453);
}

float simplex(vec2 p) {
    const float K1 = 0.366025404; // (sqrt(3)-1)/2
    const float K2 = 0.211324865; // (3-sqrt(3))/6

    vec2 i = floor(p + (p.x + p.y) * K1);
    vec2 a = p - i + (i.x + i.y) * K2;
    float m = step(a.y, a.x);
    vec2 o = vec2(m, 1.0 - m);
    vec2 b = a - o + K2;
    vec2 c = a - 1.0 + 2.0 * K2;

    vec3 h = max(0.5 - vec3(dot(a, a), dot(b, b), dot(c, c)), 0.0);
    vec3 n = h * h * h * h * vec3(
        dot(a, hash2(i + 0.0)),
        dot(b, hash2(i + o)),
        dot(c, hash2(i + 1.0))
    );

    return 0.5 + 0.5 * dot(n, vec3(70.0));
}

// 旋转矩阵（每层FBM应用不同角度，进一步打破方向性）
mat2 rot2(float a) {
    return mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a)));
}

float fbm(vec2 p) {
    float v = 0.0;
    float amp = 0.5;
    mat2 r1 = rot2(0.5);
    mat2 r2 = rot2(1.3);
    mat2 r3 = rot2(2.1);
    v += simplex(p) * amp; amp *= 0.5; p = r1 * p * 2.03;
    v += simplex(p) * amp; amp *= 0.5; p = r2 * p * 2.07;
    v += simplex(p) * amp; amp *= 0.5; p = r3 * p * 2.11;
    v += simplex(p) * amp;
    return v;
}

// 六边形 SDF
float hex_sdf(vec2 uv) {
    vec2 p = abs(uv - 0.5) * 2.0;
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

void fragment() {
    // 六边形裁切+羽化
    float sdf = hex_sdf(local_uv);
    if (sdf > feather_width) discard;
    float alpha = 1.0 - smoothstep(0.0, feather_width, sdf);

    // 世界坐标作为UV基准
    vec2 base_uv = world_pos.xz * noise_scale;

    // ─── 域扭曲（domain warp） ───
    // 用低频噪声扭曲采样位置，让噪声本身的方向性消失
    vec2 warp = vec2(
        fbm(base_uv + vec2(0.0, 0.0)),
        fbm(base_uv + vec2(5.2, 1.3))
    );
    vec2 uv = base_uv + warp_strength * (warp - 0.5);

    // 主噪声（已被warp扭曲，不会有网格痕迹）
    float n = fbm(uv);

    // 三色调色板插值
    vec3 color;
    if (n < 0.5) {
        color = mix(color_dark.rgb, color_base.rgb, smoothstep(0.20, 0.5, n));
    } else {
        color = mix(color_base.rgb, color_light.rgb, smoothstep(0.5, 0.85, n));
    }

    // 木刻墨线斑块（用第二个独立噪声+不同scale，确保不与主噪声同步）
    float ink_n = fbm(uv * 1.7 + vec2(13.7, -7.3));
    float ink = 1.0 - smoothstep(ink_threshold - 0.08, ink_threshold + 0.05, ink_n);
    color = mix(color, color_dark.rgb * 0.55, ink * ink_amount);

    // 迷雾
    color = mix(color, fog_color.rgb, fog_amount);

    ALBEDO = color;
    ALPHA = alpha;
}
