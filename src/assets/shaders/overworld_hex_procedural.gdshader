// overworld_hex_procedural.gdshader
// 程序化六边形地形 shader — 按 pattern_type 区分不同地形纹理风格。

shader_type spatial;
render_mode cull_disabled;

uniform vec4 color_base : source_color = vec4(0.55, 0.45, 0.30, 1.0);
uniform vec4 color_dark : source_color = vec4(0.30, 0.22, 0.15, 1.0);
uniform vec4 color_light : source_color = vec4(0.75, 0.65, 0.45, 1.0);

// 0=平原, 1=森林, 2=水域, 3=山地, 4=沙漠, 5=雪地, 6=道路, 7=沼泽
uniform int pattern_type = 0;

uniform float noise_scale : hint_range(0.05, 4.0) = 0.6;
uniform float warp_strength : hint_range(0.0, 2.0) = 0.8;
uniform float ink_amount : hint_range(0.0, 1.0) = 0.30;
uniform float ink_threshold : hint_range(0.0, 1.0) = 0.42;
uniform float feather_width : hint_range(0.0, 0.3) = 0.03;
uniform float fog_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 fog_color : source_color = vec4(0.1, 0.12, 0.15, 1.0);

// --- 高程光照（hillshade）---
uniform vec3 sun_direction = vec3(-0.5, 0.7, -0.5);
uniform float hillshade_intensity : hint_range(0.0, 0.5) = 0.18;

varying vec3 world_pos;
varying vec2 local_uv;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    local_uv = UV;
}

vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return -1.0 + 2.0 * fract(sin(p) * 43758.5453);
}

float simplex(vec2 p) {
    const float K1 = 0.366025404;
    const float K2 = 0.211324865;
    vec2 i = floor(p + (p.x + p.y) * K1);
    vec2 a = p - i + (i.x + i.y) * K2;
    float m = step(a.y, a.x);
    vec2 o = vec2(m, 1.0 - m);
    vec2 b = a - o + K2;
    vec2 c = a - 1.0 + 2.0 * K2;
    vec3 h = max(0.5 - vec3(dot(a, a), dot(b, b), dot(c, c)), 0.0);
    vec3 n = h * h * h * h * vec3(dot(a, hash2(i)), dot(b, hash2(i + o)), dot(c, hash2(i + 1.0)));
    return 0.5 + 0.5 * dot(n, vec3(70.0));
}

mat2 rot2(float a) { return mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a))); }

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

// LOD 衰减：当像素覆盖的 uv 范围 > 阈值时，把高频细节淡出
// 用法：detail_amp = lod_fade(uv, freq) → 把这个值乘到高频项上，缩小时自动 → 0
float lod_fade(vec2 uv, float freq) {
    // 1 个像素覆盖的 uv 距离（屏幕空间投影）
    float pixelSize = max(length(dFdx(uv)), length(dFdy(uv)));
    // 该频率项的周期
    float period = 1.0 / freq;
    // 当 1 像素覆盖 > 半周期时（Nyquist 极限）开始淡出
    float ratio = pixelSize / (period * 0.5);
    return clamp(1.0 - ratio, 0.0, 1.0);
}

float hex_sdf(vec2 uv) {
    vec2 p = abs(uv - 0.5) * 2.0;
    return max(dot(p, vec2(0.866025, 0.5)), p.y) - 1.0;
}

// 0: 平原 - 域扭曲斑块
float pattern_plains(vec2 uv) {
    vec2 warp = vec2(fbm(uv), fbm(uv + vec2(5.2, 1.3)));
    return fbm(uv + warp_strength * (warp - 0.5));
}

// 1: 森林 - 密集圆点（树冠俯视感，强对比）
// 缩小时高频 dots 项淡出避免摩尔纹
float pattern_forest(vec2 uv) {
    // 大尺度光照变化（始终保留）
    float n_macro = fbm(uv * 0.6);
    // 中尺度纹理扰动
    float n_noise = fbm(uv * 2.0);
    // 高频 dots（树冠斑点）— 缩小时淡出
    float dotsFade = lod_fade(uv, 4.5);
    float dotsValue = 0.0;
    if (dotsFade > 0.01) {
        float n_dots = simplex(uv * 4.5);
        dotsValue = smoothstep(0.45, 0.65, n_dots) * dotsFade;
    }
    // dots 淡出时用 macro 填充，保留色调
    float macroBase = n_macro * 0.4 + 0.3;
    return mix(macroBase, dotsValue + macroBase * 0.5, 0.7) + n_noise * 0.1;
}

// 2: 水域 - 不规则手绘波纹（多尺度交错）
// 高频 wave 项缩小时淡出
float pattern_water(vec2 uv) {
    // 多层不同方向不同频率的波，再用噪声扰动
    vec2 distortion = vec2(fbm(uv * 0.6), fbm(uv * 0.6 + vec2(7.0, 3.0))) * 0.6;
    vec2 wuv = uv + distortion;

    // 三层波纹：不同方向叠加（高频项受 LOD 控制）
    float waveFade = lod_fade(uv, 5.0);
    float w1 = sin(wuv.y * 3.5 + wuv.x * 0.3) * 0.5 + 0.5;
    float w2 = sin(wuv.x * 2.0 - wuv.y * 0.8 + 1.5) * 0.5 + 0.5;
    float w3 = sin((wuv.x + wuv.y) * 5.0) * 0.5 + 0.5 * waveFade;

    // 浮点波纹（小白沫）— 高频，缩小淡出
    float foamFade = lod_fade(uv, 3.0);
    float foam = smoothstep(0.85, 0.95, fbm(uv * 3.0)) * 0.3 * foamFade;

    return (w1 * 0.4 + w2 * 0.3 + w3 * 0.3) * 0.7 + foam + 0.15;
}

// 3: 山地 - 锐利锯齿
// 高频 detail 项缩小时淡出
float pattern_mountain(vec2 uv) {
    float n = fbm(uv * 1.3);
    float ridge = 1.0 - abs(2.0 * n - 1.0);
    ridge = pow(ridge, 1.5);
    // 高频细节淡出避免锯齿摩尔
    float detailFade = lod_fade(uv, 4.0);
    float detail = fbm(uv * 4.0) * 0.15 * detailFade;
    return ridge * 0.85 + detail;
}

// 4: 沙漠 - 风蚀沙丘（多尺度 FBM + 域扭曲，避免规则 sin 波纹）
float pattern_sand(vec2 uv) {
    // 沙丘是"风蚀"形成的拉长波纹，不是规则正弦。
    // 用 anisotropic FBM：让 X 方向频率高于 Y → 形成横向条带感
    vec2 anisoUv = uv * vec2(1.6, 0.6);

    // 域扭曲：让条带边缘弯曲，避免直线
    vec2 warp = vec2(fbm(uv * 0.5), fbm(uv * 0.5 + vec2(4.7, 9.3)));
    anisoUv += (warp - 0.5) * 0.6;

    // 主条纹层
    float dunes = fbm(anisoUv);

    // 细沙颗粒高频细节（缩小时淡出）
    float grainFade = lod_fade(uv, 6.0);
    float grain = fbm(uv * 6.0) * 0.08 * grainFade;

    // 沙地整体偏亮，add bias 0.10
    return dunes * 0.85 + grain + 0.10;
}

// 5: 雪地/冰 - 平整加少量起伏
float pattern_snow(vec2 uv) {
    float n = fbm(uv * 0.7);
    // 偏亮，给个底色
    return smoothstep(0.25, 0.65, n) * 0.35 + 0.55;
}

// 6: 道路 - 沿一个方向的纹理
// 高频车辙 sin 缩小时淡出避免摩尔
float pattern_road(vec2 uv) {
    float rutFade = lod_fade(uv, 8.0);
    float rut1 = sin(uv.y * 8.0) * 0.5 + 0.5;
    float rut2 = sin(uv.y * 8.0 + 1.0) * 0.5 + 0.5;
    float ruts = max(rut1, rut2) * rutFade;
    // 沿路方向的颗粒石子（高频纵向）
    float pebbleFade = lod_fade(uv, 4.0);
    float pebbles = fbm(uv * vec2(0.5, 4.0)) * mix(0.6, 1.0, pebbleFade);
    return mix(pebbles, ruts, 0.5 * rutFade) * 0.7 + 0.2;
}

// 7: 沼泽 - 水洼斑块
float pattern_swamp(vec2 uv) {
    vec2 warp = vec2(fbm(uv * 1.2), fbm(uv * 1.2 + vec2(7.3, 2.1)));
    float base = fbm(uv + warp_strength * 1.5 * (warp - 0.5));
    // 突出水洼的对比
    if (base < 0.40) return base * 0.4;
    if (base > 0.60) return 0.7 + (base - 0.60) * 0.5;
    return base;
}

float get_pattern_value(vec2 uv) {
    if (pattern_type == 1) return pattern_forest(uv);
    if (pattern_type == 2) return pattern_water(uv);
    if (pattern_type == 3) return pattern_mountain(uv);
    if (pattern_type == 4) return pattern_sand(uv);
    if (pattern_type == 5) return pattern_snow(uv);
    if (pattern_type == 6) return pattern_road(uv);
    if (pattern_type == 7) return pattern_swamp(uv);
    return pattern_plains(uv);
}

void fragment() {
    // --- 裙边检测：UV.y > 1.0 表示侧面 ---
    if (local_uv.y > 1.0) {
        vec3 cliff_top = vec3(0.30, 0.22, 0.15);
        vec3 cliff_bot = vec3(0.15, 0.12, 0.08);
        vec3 cliff_color = mix(cliff_top, cliff_bot, local_uv.x);
        float cliff_shade = clamp(world_pos.y * 0.4, -0.15, 0.1);
        cliff_color *= 1.0 + cliff_shade;
        ALBEDO = cliff_color;
        ALPHA = 1.0;
        return;
    }

    float sdf = hex_sdf(local_uv);
    if (sdf > feather_width) discard;
    float alpha = 1.0 - smoothstep(0.0, feather_width, sdf);

    vec2 uv = world_pos.xz * noise_scale;
    float n = get_pattern_value(uv);

    vec3 color;
    if (n < 0.5) {
        color = mix(color_dark.rgb, color_base.rgb, smoothstep(0.15, 0.5, n));
    } else {
        color = mix(color_base.rgb, color_light.rgb, smoothstep(0.5, 0.88, n));
    }

    if (ink_amount > 0.01) {
        // 墨线高频细节，缩小时淡出避免颗粒摩尔纹
        float inkFade = lod_fade(uv, 1.7);
        float ink_n = fbm(uv * 1.7 + vec2(13.7, -7.3));
        float ink = 1.0 - smoothstep(ink_threshold - 0.08, ink_threshold + 0.05, ink_n);
        color = mix(color, color_dark.rgb * 0.55, ink * ink_amount * inkFade);
    }

    // --- 高程光照（hillshade）---
    {
        float directional = dot(normalize(sun_direction.xz), world_pos.xz * 0.1) * 0.3;
        float heightFactor = world_pos.y * 1.2;
        float shade = clamp(heightFactor + directional, -1.0, 1.0) * hillshade_intensity;
        color *= 1.0 + shade;
    }

    color = mix(color, fog_color.rgb, fog_amount);
    ALBEDO = color;
    ALPHA = alpha;
}
