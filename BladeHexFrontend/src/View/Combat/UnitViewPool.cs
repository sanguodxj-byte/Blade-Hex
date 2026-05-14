// UnitViewPool.cs — 通用对象池 + 战斗视图池
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Combat;

public class NodePool<T> where T : Node
{
    private readonly Stack<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _onRetrieve;
    private readonly Action<T>? _onReturn;
    private readonly int _maxSize;
    private Node? _parent;

    public int ActiveCount { get; private set; }
    public int PooledCount => _pool.Count;

    public NodePool(Func<T> factory, Action<T>? onRetrieve = null, Action<T>? onReturn = null, int maxSize = 64)
    {
        _factory = factory;
        _onRetrieve = onRetrieve;
        _onReturn = onReturn;
        _maxSize = maxSize;
    }

    public void SetParent(Node parent) => _parent = parent;

    public T Retrieve()
    {
        var item = _pool.Count > 0 ? _pool.Pop() : _factory();
        _onRetrieve?.Invoke(item);
        if (_parent != null && item.GetParent() == null) _parent.AddChild(item);
        ActiveCount++;
        return item;
    }

    public void Return(T item)
    {
        if (item == null || !GodotObject.IsInstanceValid(item)) return;
        _onReturn?.Invoke(item);
        if (item.GetParent() != null) item.GetParent().RemoveChild(item);
        if (_pool.Count < _maxSize) _pool.Push(item); else item.QueueFree();
        ActiveCount--;
    }

    public void Prewarm(int count) { for (int i = 0; i < count; i++) _pool.Push(_factory()); }

    public void Clear()
    {
        foreach (var item in _pool) if (GodotObject.IsInstanceValid(item)) item.QueueFree();
        _pool.Clear();
        ActiveCount = 0;
    }
}

[GlobalClass]
public partial class UnitViewPool : Node
{
    private NodePool<Sprite3D>? _spritePool;
    private NodePool<Label3D>? _labelPool;
    private NodePool<AnimatedSprite3D>? _animPool;

    public override void _Ready()
    {
        _spritePool = new NodePool<Sprite3D>(
            () => new Sprite3D(),
            s => { s.Visible = true; s.Texture = null; },
            s => { s.Visible = false; s.Texture = null; s.Position = Vector3.Zero; },
            maxSize: 48);

        _labelPool = new NodePool<Label3D>(
            () => new Label3D { PixelSize = 3.0f },
            l => l.Visible = true,
            l => { l.Visible = false; l.Text = ""; },
            maxSize: 48);

        _animPool = new NodePool<AnimatedSprite3D>(
            () => new AnimatedSprite3D { PixelSize = 1.0f },
            a => a.Visible = true,
            a => { a.Visible = false; a.SpriteFrames = null; a.Stop(); },
            maxSize: 48);
    }

    public Sprite3D RetrieveSprite() => _spritePool!.Retrieve();
    public void ReturnSprite(Sprite3D s) => _spritePool!.Return(s);
    public Label3D RetrieveLabel() => _labelPool!.Retrieve();
    public void ReturnLabel(Label3D l) => _labelPool!.Return(l);
    public AnimatedSprite3D RetrieveAnimSprite() => _animPool!.Retrieve();
    public void ReturnAnimSprite(AnimatedSprite3D a) => _animPool!.Return(a);
}
