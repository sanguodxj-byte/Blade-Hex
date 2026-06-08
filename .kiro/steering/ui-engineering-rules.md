---
inclusion: fileMatch
fileMatchPattern: '**/View/UI/**/*.cs'
---

# UI 工程规约（防错优先）

> 全文索引在 `docs/09-UI设计.md` 第十一节。这里给出最常踩的 3 类坑和模板代码，写 / 改 UI 时务必对照。

## 1. 自定义 _Draw 中的 DrawString 必须先 box 后 text

不要传 `width=-1` 给 `DrawString`，否则 `HorizontalAlignment.Center` 不生效，文字会漂在 box 外。

正确顺序：
1. `font.GetStringSize(text, Left, -1, fs)` 测真实尺寸
2. 用 `textSize + padding*2` 算 `boxSize`，`boxPos` 是左上角
3. 先画 box（背景/边框/高光）
4. 再画 text：`pos.X = boxPos.X`、`width = boxSize.X`、`HorizontalAlignment.Center`、`pos.Y = boxPos.Y + padY + font.GetAscent(fs)`

## 2. 受容器约束的 RichText / Label 必须开启换行

```csharp
rt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
rt.FitContent = false;        // 不撑大父容器
rt.ScrollActive = true;       // 内容超长时面板内滚动
rt.SizeFlagsHorizontal = SizeFlags.Fill;  // 不外扩
```

父 PanelContainer 侧：
```csharp
panel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;  // 严格保持固定宽度
panel.ClipContents = true;                           // 兜底裁切越界
```

`UIFactory.CreateRichText` 默认 `FitContent=true`、不开 AutowrapMode；调用方必须自行覆盖。

## 3. 模态/全屏面板必须事件驱动 + 全局拦截输入

铁律：键鼠事件用 `_UnhandledInput` 处理，最后必须 `GetViewport().SetInputAsHandled()`。
- 禁止 `Input.IsKeyPressed` 全局轮询：下层场景的 `_Process` 一样能读到
- WASD/方向键：在 `_UnhandledInput` 里维护内部 `_moveUp/Down/Left/Right` bool，`_Process` 读这个状态算移动
- 滚轮：在 `_UnhandledInput` 而非仅 `GuiInput` 拦截，画布内做缩放、画布外吃掉防穿透
- 关闭面板（ESC / 关闭按钮 / Visible=false）必须清零所有 held 状态

## 4. 写完面板后的自查清单

- [ ] 所有 `DrawString` 都传了 `width > 0`，且 `position.X = boxLeft`
- [ ] 所有 `RichTextLabel` 都设了 `AutowrapMode`（除非确实需要单行）
- [ ] 受固定宽度约束的子节点用 `SizeFlags.Fill` 而非 `ExpandFill`
- [ ] 模态面板的 `_UnhandledInput` 拦截 ESC + WASD + 滚轮，最后 `SetInputAsHandled()`
- [ ] 所有关闭路径都清零内部按住状态
- [ ] `_Process` 没用 `Input.IsKeyPressed` 读移动键
- [ ] 自定义 `_Draw` 的容器设了 `ClipContents = true`
- [ ] 模态面板根节点设了 `MouseFilter = Stop`
