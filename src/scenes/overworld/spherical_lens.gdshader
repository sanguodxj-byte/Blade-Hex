shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float distortion_strength : hint_range(-1.0, 1.0) = 0.08;

void fragment() {
    vec2 center = vec2(0.5, 0.5);
    vec2 uv = SCREEN_UV;
    
    vec2 offset = uv - center;
    float dist = length(offset);
    
    // 简单的桶形畸变 (Barrel Distortion) 模拟微弱的地球仪/球形透视感
    uv = center + offset * (1.0 - dist * distortion_strength);
    
    COLOR = texture(screen_texture, uv);
}