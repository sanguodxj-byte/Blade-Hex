shader_type canvas_item;

// 按钮状态: 0.0 = normal, 1.0 = hover, 2.0 = pressed
uniform float state : hint_range(0.0, 2.0) = 0.0;
// 边框发光颜色 (hover时)
uniform vec4 glow_color : source_color = vec4(0.85, 0.72, 0.35, 0.8);
// 边框发光宽度
uniform float glow_width : hint_range(0.0, 0.05) = 0.02;
// 按下时的内阴影强度
uniform float press_shadow : hint_range(0.0, 0.5) = 0.15;

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    vec4 result = tex;

    // ── Normal: 原样输出 ──

    // ── Hover: 提亮 + 边缘发光 ──
    if (state > 0.4 && state < 1.6) {
        // 整体微微提亮
        result.rgb *= 1.12;

        // 边缘发光 (基于UV距离边缘的距离)
        float edge_x = min(UV.x, 1.0 - UV.x);
        float edge_y = min(UV.y, 1.0 - UV.y);
        float edge_dist = min(edge_x, edge_y);

        if (edge_dist < glow_width) {
            float glow_factor = 1.0 - (edge_dist / glow_width);
            glow_factor = glow_factor * glow_factor; // 平方衰减更自然
            result.rgb = mix(result.rgb, glow_color.rgb, glow_factor * glow_color.a);
        }
    }

    // ── Pressed: 压暗 + 内阴影 ──
    if (state > 1.4) {
        // 整体压暗
        result.rgb *= 0.82;

        // 内阴影 (从上方和左方)
        float shadow_top = smoothstep(0.0, 0.15, UV.y);
        float shadow_left = smoothstep(0.0, 0.1, UV.x);
        float shadow = min(shadow_top, shadow_left);
        result.rgb *= mix(1.0 - press_shadow, 1.0, shadow);

        // 轻微去饱和
        float grey = dot(result.rgb, vec3(0.299, 0.587, 0.114));
        result.rgb = mix(vec3(grey), result.rgb, 0.85);
    }

    COLOR = result;
}
