shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform vec4 outline_color : source_color = vec4(1.0, 0.85, 0.0, 1.0);
uniform float line_thickness : hint_range(0.5, 10.0) = 2.0;
uniform bool show_outline = true;

uniform sampler2D texture_albedo : source_color, hint_default_white;

const vec2 OFFSETS[8] = {
    vec2(-1.0, -1.0), vec2(-1.0, 0.0), vec2(-1.0, 1.0), vec2(0.0, -1.0),
    vec2(0.0, 1.0), vec2(1.0, -1.0), vec2(1.0, 0.0), vec2(1.0, 1.0)
};

void fragment() {
    vec4 color = texture(texture_albedo, UV);
    
    if (!show_outline) {
        ALBEDO = color.rgb;
        ALPHA = color.a;
        return;
    }

    vec2 pixel_size = (1.0 / vec2(textureSize(texture_albedo, 0))) * line_thickness;
    float outline = 0.0;
    for (int i = 0; i < 8; i++) {
        outline += texture(texture_albedo, UV + pixel_size * OFFSETS[i]).a;
    }
    outline = min(outline, 1.0);
    float mix_factor = clamp(outline - color.a, 0.0, 1.0);
    
    ALBEDO = mix(color, outline_color, mix_factor).rgb;
    ALPHA = max(color.a, outline * mix_factor);
}
