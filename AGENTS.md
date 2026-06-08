# Agent Instructions for Blade&Hex

## Godot shader work

When a task creates, modifies, reviews, or diagnoses any `.gdshader` or
`.gdshaderinc` file, the shader context is mandatory. Before editing or giving a
shader implementation, read:

1. `docs/godot_gdshader_authoring.md`
2. `src/assets/shaders/GDSHADER_CONTEXT.md`

Apply the local hard rules from those files even when a GLSL example from the
internet appears to compile elsewhere. Godot shaders are GLSL-like, but they are
not raw GLSL programs.

After shader edits, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "src\assets\shaders\lint_godot_shaders.ps1"
```

Also, you MUST verify full compilation using the build-blocking compiler validator:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\shader_validator\verify_shaders.ps1"
```

Never leave `return` inside Godot processor functions such as `vertex()`,
`fragment()`, `light()`, `start()`, `process()`, `sky()`, or `fog()`.
