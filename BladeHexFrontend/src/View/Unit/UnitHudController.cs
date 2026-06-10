using BladeHex.Data;
using BladeHex.View.AssetSystem;
using Godot;

namespace BladeHex.Combat;

[GlobalClass]
public partial class UnitHudController : Node3D
{
    private const string HudSceneId = "unit_hud";
    private const string HudScenePath = "res://BladeHexFrontend/src/View/Unit/UnitHud.tscn";

    private const float SelectionRingRadius = 40.0f;
    private const float SelectionRingHeight = 5.0f;
    private const float HpLabelYGap = 20.0f;
    private const float HpLabelPixelSize = 3.0f;
    private const float HpBarYGap = 15.0f;
    private const float HpBarWidth = 60.0f;
    private const float HpBarHeight = 4.0f;
    private const float TurnIndicatorYGap = 10.0f;
    private const float StatusIconSize = 16.0f;
    private const float StatusIconSpacing = 20.0f;
    private const int BuffIconTexSize = 32;

    private static ImageTexture? _buffIconTex;

    private Label3D? _hpLabel;
    private MeshInstance3D? _hpBarBg;
    private MeshInstance3D? _hpBarFg;
    private Node3D? _statusContainer;

    private int _currentHp;
    private int _maxHp = 1;
    private float _cachedBodyHeight;
    private float _cachedPixelSize;
    private bool _isSelected;
    private bool _isActiveTurn;

    [Signal] public delegate void HpChangedEventHandler(int currentHp, int maxHp);

    public void Build(float bodyHeight, float pixelSize)
    {
        _cachedBodyHeight = bodyHeight;
        _cachedPixelSize = pixelSize;

        var hudScene = PackedSceneAssetResolver.Load(HudSceneId, HudScenePath);
        if (hudScene == null)
        {
            GD.PushError($"[UnitHudController] Failed to load HUD scene: {HudScenePath}");
            return;
        }

        var hudInstance = hudScene.Instantiate<Node3D>();
        hudInstance.Name = "Hud";
        AddChild(hudInstance);

        _hpLabel = hudInstance.GetNode<Label3D>("%HpLabel");
        _hpBarBg = hudInstance.GetNode<MeshInstance3D>("%HpBarBg");
        _hpBarFg = hudInstance.GetNode<MeshInstance3D>("%HpBarFg");
        _statusContainer = hudInstance.GetNode<Node3D>("%StatusContainer");

        if (_hpLabel != null) _hpLabel.Visible = false;
        if (_hpBarBg != null) _hpBarBg.Visible = false;
        if (_hpBarFg != null) _hpBarFg.Visible = false;

        ApplyLayout();
    }

    public void UpdateHp(int current, int maximum)
    {
        _currentHp = current;
        _maxHp = Mathf.Max(1, maximum);
        EmitSignal(SignalName.HpChanged, _currentHp, _maxHp);
    }

    public void UpdateStatusEffects(Godot.Collections.Array effects)
    {
        if (_statusContainer == null)
            return;

        foreach (var child in _statusContainer.GetChildren())
            child.QueueFree();

        for (int i = 0; i < effects.Count; i++)
            _statusContainer.AddChild(MakeStatusIcon(effects[i], i));
    }

    public void SetSelected(bool on)
    {
        _isSelected = on;
    }

    public void SetActiveTurn(bool on)
    {
        _isActiveTurn = on;
    }

    public void HideAll()
    {
        if (_hpBarBg != null) _hpBarBg.Visible = false;
        if (_hpBarFg != null) _hpBarFg.Visible = false;
        if (_hpLabel != null) _hpLabel.Visible = false;
        if (_statusContainer != null) _statusContainer.Visible = false;
    }



    private void ApplyLayout()
    {
        float topY = _cachedBodyHeight * _cachedPixelSize;

        if (_hpLabel != null)
            _hpLabel.Position = new Vector3(0, topY + HpLabelYGap, 0);

        float barY = topY + HpBarYGap;
        if (_hpBarBg != null)
        {
            _hpBarBg.Mesh = new QuadMesh { Size = new Vector2(HpBarWidth, HpBarHeight) };
            _hpBarBg.Position = new Vector3(0, barY, 0);
            _hpBarBg.MaterialOverride = MakeHudMaterial(new Color(0.2f, 0.2f, 0.2f, 0.8f));
        }

        if (_hpBarFg != null)
        {
            _hpBarFg.Mesh = new QuadMesh { Size = new Vector2(HpBarWidth, HpBarHeight) };
            _hpBarFg.Position = new Vector3(0, barY, -0.1f);
            _hpBarFg.MaterialOverride = MakeHudMaterial(new Color(0.2f, 0.8f, 0.2f));
        }

        if (_statusContainer != null)
            _statusContainer.Position = new Vector3(0, topY + HpBarYGap + HpBarHeight + 5.0f, 0);


    }

    private static StandardMaterial3D MakeHudMaterial(Color color) => new()
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
        RenderPriority = 10,
        AlbedoColor = color,
    };

    private Sprite3D MakeStatusIcon(Variant effectVar, int index)
    {
        var icon = new Sprite3D
        {
            Texture = GetBuffIconTexture(),
            PixelSize = 0.5f,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            Offset = new Vector2(0, StatusIconSize / 2.0f),
            Position = new Vector3((index - 2.0f) * StatusIconSpacing, 0, 0),
        };

        if (effectVar.VariantType == Variant.Type.Dictionary)
        {
            var dict = effectVar.AsGodotDictionary();
            var id = dict.TryGetValue("id", out var idVar) ? idVar.AsString() : "";
            icon.Modulate = StatusColor(id);
        }

        return icon;
    }

    private static ImageTexture GetBuffIconTexture()
    {
        if (_buffIconTex != null)
            return _buffIconTex;

        var img = Image.CreateEmpty(BuffIconTexSize, BuffIconTexSize, false, Image.Format.Rgba8);
        byte[] data = new byte[BuffIconTexSize * BuffIconTexSize * 4];
        float cx = BuffIconTexSize / 2f;
        float cy = BuffIconTexSize / 2f;
        float r = BuffIconTexSize / 2f - 1;

        for (int y = 0; y < BuffIconTexSize; y++)
        {
            for (int x = 0; x < BuffIconTexSize; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = (y * BuffIconTexSize + x) * 4;
                if (dist <= r)
                {
                    data[idx] = 255;
                    data[idx + 1] = 255;
                    data[idx + 2] = 255;
                    data[idx + 3] = 255;
                }
                else
                {
                    data[idx + 3] = 0;
                }
            }
        }

        img.SetData(BuffIconTexSize, BuffIconTexSize, false, Image.Format.Rgba8, data);
        _buffIconTex = ImageTexture.CreateFromImage(img);
        return _buffIconTex;
    }

    private static Color StatusColor(string id) => id switch
    {
        "burning" => new Color(1.0f, 0.4f, 0.1f),
        "freeze" or "frozen" => new Color(0.3f, 0.6f, 1.0f),
        "poison" or "poisoned" => new Color(0.4f, 0.8f, 0.2f),
        "entangled" or "web" => new Color(0.6f, 0.4f, 0.2f),
        "stun" or "stunned" => new Color(0.9f, 0.9f, 0.2f),
        "charmed" => new Color(1.0f, 0.5f, 0.8f),
        "bleed" or "bleeding" => new Color(0.8f, 0.1f, 0.1f),
        "shield" or "magic_shield" => new Color(0.3f, 0.5f, 1.0f),
        "blessing" => new Color(1.0f, 1.0f, 0.7f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };
}
