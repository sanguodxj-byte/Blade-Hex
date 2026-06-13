shader_type spatial;
render_mode cull_disabled;

uniform sampler2D tex_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D detail_tex_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform bool use_detail_texture = false;
uniform float detail_world_scale : hint_range(0.001, 0.2) = 0.05;
uniform float detail_strength : hint_range(0.0, 1.0) = 0.18;
uniform float detail_contrast : hint_range(0.0, 2.0) = 1.0;

uniform int cliff_mask = 0;
uniform float apothem = 83.138;
uniform float half_extent = 115.2;
uniform vec4 modulate : source_color = vec4(1.0);
uniform float fake_light_strength : hint_range(0.0, 0.6) = 0.16;
uniform float fake_light_world_scale : hint_range(0.001, 0.02) = 0.0045;
uniform vec4 fake_light_warm : source_color = vec4(1.08, 1.02, 0.92, 1.0);
uniform vec4 fake_light_cool : source_color = vec4(0.82, 0.88, 1.05, 1.0);
uniform float forest_dapple_strength : hint_range(0.0, 1.0) = 0.0;
uniform float forest_dapple_scale : hint_range(0.004, 0.04) = 0.014;
uniform vec4 forest_dapple_color : source_color = vec4(1.20, 1.08, 0.78, 1.0);

uniform bool use_battlefield_uv = true;
uniform vec2 uv_world_origin = vec2(0.0, 0.0);
uniform vec2 uv_world_size = vec2(1.0, 1.0);
uniform vec2 uv_min = vec2(0.075, 0.075);
uniform vec2 uv_max = vec2(0.925, 0.925);
uniform float hex_edge_feather = 14.0;

varying vec3 v_world_pos;
varying vec2 v_instance_rand;

vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return fract(sin(p) * 43758.5453);
}

float hash21(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise2d(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float hex_sdf(vec2 v, float radius) {
    v = abs(v);
    return max(dot(v, vec2(0.866025, 0.5)), v.y) - radius;
}

void vertex() {
    v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    v_instance_rand = hash2(MODEL_MATRIX[3].xz);
}

vec2 resolve_macro_uv() {
    if (!use_battlefield_uv) {
        return v_world_pos.xz * 0.25 / max(vec2(half_extent * 2.0), vec2(1.0));
    }

    vec2 uv01 = (v_world_pos.xz - uv_world_origin) / max(uv_world_size, vec2(1.0, 1.0));
    return mix(uv_min, uv_max, clamp(uv01, vec2(0.0), vec2(1.0)));
}

void fragment() {
    vec2 p = (UV - vec2(0.5)) * (half_extent * 2.0);

    for (int d = 0; d < 6; d++) {
        if ((cliff_mask & (1 << d)) != 0) {
            float a = radians(30.0 - 60.0 * float(d));
            vec2 n = vec2(cos(a), sin(a));
            if (dot(p, n) > apothem) {
                discard;
            }
        }
    }

    vec2 macro_uv = resolve_macro_uv();
    vec4 base_sample = texture(tex_albedo, macro_uv) * modulate;

    vec3 rgb = base_sample.rgb;
    if (use_detail_texture) {
        vec2 detail_uv = (v_world_pos.xz - uv_world_origin) * detail_world_scale;
        detail_uv += (v_instance_rand - vec2(0.5)) * 0.11;

        vec3 detail_sample = texture(detail_tex_albedo, detail_uv).rgb;
        float detail_luma = dot(detail_sample, vec3(0.299, 0.587, 0.114));
        float detail_mix = clamp(0.5 + (detail_luma - 0.5) * detail_contrast, 0.0, 1.0);
        float detail_factor = mix(1.0 - detail_strength, 1.0 + detail_strength, detail_mix);

        rgb *= detail_factor;
        rgb += (detail_sample - vec3(detail_luma)) * (detail_strength * 0.08);
    }

    float hex_radius_world = apothem * 1.1547005;
    float dist = hex_sdf(p, hex_radius_world);
    float sdf_alpha = 1.0 - smoothstep(0.0, hex_edge_feather, dist);

    float low_noise = noise2d(v_world_pos.xz * fake_light_world_scale);
    float ridge = sin(dot(v_world_pos.xz, vec2(0.005, -0.003)) + low_noise * 2.0);
    float light_mix = clamp(low_noise * 0.65 + ridge * 0.18 + v_instance_rand.x * 0.17, 0.0, 1.0);
    vec3 fake_tint = mix(fake_light_cool.rgb, fake_light_warm.rgb, light_mix);
    float shade = mix(-1.0, 1.0, light_mix) * fake_light_strength;

    float dapple_a = noise2d(v_world_pos.xz * forest_dapple_scale + vec2(13.1, 7.7));
    float dapple_b = noise2d(v_world_pos.xz * forest_dapple_scale * 2.7 + vec2(31.0, 19.0));
    float dapple_mask = smoothstep(0.56, 0.82, dapple_a) * smoothstep(0.30, 0.74, dapple_b);
    vec3 dapple_tint = mix(vec3(1.0), forest_dapple_color.rgb, dapple_mask * forest_dapple_strength);
    float dapple_light = 1.0 + dapple_mask * forest_dapple_strength * 0.45;

    ALBEDO = clamp(rgb * fake_tint * (1.0 + shade) * dapple_tint * dapple_light, vec3(0.0), vec3(1.0));
    ALPHA = base_sample.a * sdf_alpha;
}
