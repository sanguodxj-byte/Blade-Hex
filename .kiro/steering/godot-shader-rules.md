---
inclusion: fileMatch
fileMatchPattern: "**/*.gdshader"
---

# Godot 4 Shader 编写规则

本项目使用 Godot 4.6 + Forward Mobile 渲染器 (D3D12)。编写或修改 `.gdshader` 文件时必须遵守以下规则。

## 严格禁止

1. **禁止在 `fragment()`、`vertex()`、`light()` 函数中使用 `return;` 语句**
   - Godot shader 语言不支持在这些入口函数中提前返回
   - 必须使用 `if/else` 结构代替
   - `discard;` 是允许的（仅在 fragment 中）

   ```gdshader
   // ❌ 错误
   void fragment() {
       if (condition) {
           ALBEDO = color1;
           return;  // 编译错误！
       }
       ALBEDO = color2;
   }

   // ✅ 正确
   void fragment() {
       if (condition) {
           ALBEDO = color1;
       } else {
           ALBEDO = color2;
       }
   }
   ```

2. **禁止使用 `textureSize()` 函数**
   - Mobile 渲染器不支持此函数
   - 改用 uniform 从 C# 传入纹理尺寸

   ```gdshader
   // ❌ 错误
   vec2 size = vec2(textureSize(my_texture, 0));

   // ✅ 正确
   uniform vec2 texture_size = vec2(256.0, 256.0);
   ```

3. **禁止在自定义函数中使用 `out` 参数修改 shader 内置变量**

## 注意事项

- `render_mode` 中 `cull_disabled` 是允许的
- `varying` 变量在 vertex 和 fragment 之间传递是允许的
- `const` 全局变量（包括 `const mat2`）是允许的
- 嵌套 for 循环（编译时常量边界）是允许的，但应控制总迭代次数 < 64
- `dFdx()` / `dFdy()` 在 Mobile 渲染器中是支持的
- `discard` 仅在 `fragment()` 中允许使用

## 项目 shader 文件清单

- `src/assets/shaders/overworld_hex_textured.gdshader` — 大地图地形纹理
- `src/assets/shaders/overworld_hex_procedural.gdshader` — 大地图程序化地形
- `src/assets/shaders/overworld_poi_hex.gdshader` — POI 覆盖层
- `FogOverlay3D.cs` 内联 shader — 迷雾覆盖层（注意：内联在 C# 字符串中）

## 渲染器信息

- 渲染方法: `renderer/rendering_method="mobile"` (Forward Mobile)
- GPU 驱动: D3D12
- 项目配置: `project.godot` → `[rendering]` 节
