// TutorialData.cs
// 教程数据模型 — 从 tutorial_pages.json 加载
using System.Collections.Generic;

namespace BladeHex.UI.Tutorial;

/// <summary>教程单页内容</summary>
public class TutorialPage
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>教程章节（一组相关页面）</summary>
public class TutorialChapter
{
    public string Id { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Title { get; set; } = "";
    public List<TutorialPage> Pages { get; set; } = new();
}

/// <summary>教程数据根</summary>
public class TutorialDataRoot
{
    public List<TutorialChapter> Chapters { get; set; } = new();
}
