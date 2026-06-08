// ICombatActionContext.cs
// 战斗场景行动上下文门面接口 — 组合四个聚焦接口，为需要广泛访问的消费者提供统一类型。

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景行动上下文门面接口。组合选择状态、高亮、行动服务、战斗结束四个聚焦接口。
/// </summary>
public interface ICombatActionContext : ICombatSelectionContext, ICombatHighlightPort, ICombatActionServices, ICombatEndPort
{
}
