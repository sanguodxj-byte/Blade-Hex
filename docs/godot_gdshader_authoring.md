# Godot 4.6 GDSHADER 编写规范与官方文档索引

本文档是 Blade&Hex 的 `.gdshader` / `.gdshaderinc` 强制上下文。任何新增、
修改、审查或诊断 shader 的任务，都必须先读取本文档和
`src/assets/shaders/GDSHADER_CONTEXT.md`。

## 官方文档索引

本项目以 Godot 4.6 为准，不按 Godot 3.x 或通用 GLSL 规则直接推断。

| 场景 | 官方文档 |
| --- | --- |
| 语言基础、类型、数组、函数、varying、discard | https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/shading_language.html |
| 2D / UI / Sprite / 地图覆盖 shader | https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/canvas_item_shader.html |
| `#include`、`.gdshaderinc`、预处理宏、renderer define | https://docs.godotengine.org/en/4.6/tutorials/shaders/shader_reference/shader_preprocessor.html |
| GLSL / Shadertoy 代码迁移到 Godot | https://docs.godotengine.org/en/4.6/tutorials/shaders/converting_glsl_to_godot_shaders.html |
| 格式、命名、编码、代码顺序 | https://docs.godotengine.org/en/4.6/tutorials/shaders/shaders_style_guide.html |

## 强制上下文注入规则

后续任何 agent 或人工流程，只要涉及 `.gdshader` 或 `.gdshaderinc`，必须在编写
前读取：

1. 本文档。
2. `src/assets/shaders/GDSHADER_CONTEXT.md`。
3. 目标 shader 及其引用的 `.gdshaderinc`。
4. 绑定该 shader 的 C# / GDScript / `.tres` / `.tscn`。

根目录 `AGENTS.md` 和 `src/assets/shaders/AGENTS.md` 已声明这条规则。对 Codex
类 agent 来说，这是仓库级强制上下文入口；对人工开发来说，这是 shader 修改前的
检查清单。

## 本项目硬规则

### 1. Processor 函数禁止 `return`

以下 Godot processor 函数内部禁止写 `return`：

- `vertex()`
- `fragment()`
- `light()`
- `start()`
- `process()`
- `sky()`
- `fog()`

这条是项目硬规则，来自 Godot 4.6 编译器实际报错：

```text
Using 'return' in the 'fragment' processor function is incorrect.
```

Godot 官方文档允许普通自定义函数返回值，但 processor 函数不是普通业务函数。
需要提前退出时，用辅助函数返回值，或者用 `if` / `else` 守卫赋值。

错误写法：

```glsl
shader_type canvas_item;

void fragment() {
	if (UV.x < 0.0) {
		return;
	}

	COLOR = texture(TEXTURE, UV);
}
```

正确写法：

```glsl
shader_type canvas_item;

bool should_draw(vec2 uv) {
	return uv.x >= 0.0;
}

void fragment() {
	if (should_draw(UV)) {
		COLOR = texture(TEXTURE, UV);
	} else {
		COLOR = vec4(0.0);
	}
}
```

### 2. 先确定 shader 类型

文件开头必须有且只有一个 `shader_type`。

常用选择：

- `canvas_item`：2D、UI、Sprite2D、ColorRect、TextureRect、大地图覆盖层。
- `spatial`：3D mesh、3D 地表、3D 材质。
- `particles` / `sky` / `fog`：只在对应 Godot 系统使用时选择。

大地图 2D、技能星盘、UI 按钮、夜间遮罩这类效果，默认使用
`shader_type canvas_item;`。

### 3. CanvasItem 输出规则

`canvas_item` 的 `fragment()` 里：

- 最终颜色写入 `COLOR`。
- 节点自身纹理使用 `texture(TEXTURE, UV)`。
- 屏幕空间采样使用 `SCREEN_UV`。
- Godot 4 不再使用旧的直接 `SCREEN_TEXTURE`，需要声明带
  `hint_screen_texture` 的 `sampler2D`。

示例：

```glsl
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

void fragment() {
	vec4 base_color = texture(TEXTURE, UV);
	vec3 screen_color = texture(screen_texture, SCREEN_UV).rgb;
	COLOR = vec4(mix(base_color.rgb, screen_color, 0.25), base_color.a);
}
```

### 4. Include 规则

- 只能 include `.gdshaderinc`，不能 include `.gdshader`。
- 相对路径只适用于保存成文件的 `.gdshader` / `.gdshaderinc`。
- 全局 include 建议放在 `shader_type` 之后。
- 禁止循环 include。
- 公共函数命名要加领域前缀，避免重名。

### 5. GLSL 迁移规则

Godot shader 是 GLSL-like，不是原生 GLSL 程序。迁移外部代码时至少检查：

| GLSL / Shadertoy | Godot |
| --- | --- |
| `main()` | `fragment()` / `vertex()` |
| `gl_FragColor` | `COLOR` |
| `gl_FragCoord` | `FRAGCOORD` |
| `gl_Position` | `VERTEX` |
| `iResolution` | 由脚本传 `uniform vec2`，或用屏幕/纹理尺寸推导 |
| `iTime` | `TIME` 或脚本传入稳定时间 |
| `sampler2D iChannel0` | Godot `uniform sampler2D` |

任何外部 shader 片段都必须先改写为 Godot 语义，再进入项目。

## 代码组织

推荐顺序：

1. `shader_type`
2. `render_mode`
3. 简短说明注释
4. `uniform`
5. `const`
6. `varying`
7. 辅助函数
8. `vertex()`
9. `fragment()`
10. `light()`

辅助函数必须写在调用点之前。不要依赖“后面定义也能调用”的 C# / JavaScript
习惯。

## 编码与格式

- UTF-8 without BOM。
- LF 换行。
- 文件末尾保留一个换行。
- 缩进使用 tab。
- 单行一个语句。
- 控制语句总是写 `{}`。
- 浮点数字写完整：`0.0`、`1.0`、`5.0`，不要写 `.5` 或 `5.`。
- 颜色向量访问用 `.r/.g/.b/.a`，坐标或通用向量用 `.x/.y/.z/.w`。

## 绑定与失败保护

shader 编译错误可能导致后续材质或渲染服务出现级联错误。处理顺序必须是：

1. 先找最早的 `SHADER ERROR`。
2. 修 shader 编译错误。
3. 再看 `version_get_shader`、RID 泄漏、材质为空等后续错误。

C# / GDScript 加载 shader 时，失败资源不得继续绑定到 `ShaderMaterial`。如果是
核心渲染路径，应该有 fallback 材质或直接跳过该特效。

## 提交前检查

从项目根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "src\assets\shaders\lint_godot_shaders.ps1"
```

如果 Godot 仍然使用旧 shader 缓存，可移动 shader cache：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "src\assets\shaders\lint_godot_shaders.ps1" -ClearShaderCache
```

`-ClearShaderCache` 会把 `.godot/shader_cache` 移到带时间戳的备份目录，不会直接
删除。

## 最小 CanvasItem 模板

```glsl
shader_type canvas_item;

uniform float strength : hint_range(0.0, 1.0) = 1.0;

vec4 apply_effect(vec4 base_color, vec2 uv) {
	float vignette = smoothstep(0.85, 0.15, distance(uv, vec2(0.5)));
	return vec4(base_color.rgb * mix(1.0, vignette, strength), base_color.a);
}

void fragment() {
	vec4 base_color = texture(TEXTURE, UV) * COLOR;
	COLOR = apply_effect(base_color, UV);
}
```

这个模板的关键点是：processor 只做赋值和流程控制，提前判断和返回值放在普通
辅助函数里。
