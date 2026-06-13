using Godot;
using BladeHex.Combat;
using BladeHex.View.AssetSystem;
using CombatUnit = BladeHex.Combat.Unit;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class BattleFakeShadowLayer : Node3D
{
    private const string ShaderPath = "res://BladeHexFrontend/src/assets/shaders/battle_contact_shadow.gdshader";
    private const string ShadowNodeName = "ContactShadow";

    public static BattleFakeShadowLayer? Instance { get; private set; }

    private static Shader? _shadowShader;

    public override void _Ready()
    {
        Instance = this;
        _shadowShader ??= ShaderAssetResolver.Load("battle_contact_shadow", ShaderPath);
        if (_shadowShader == null)
            GD.PushWarning($"[BattleFakeShadowLayer] Missing shader: {ShaderPath}");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void AttachContactShadow(CombatUnit unit)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit))
            return;

        _shadowShader ??= ShaderAssetResolver.Load("battle_contact_shadow", ShaderPath);
        if (_shadowShader == null)
            return;

        var shadow = unit.GetNodeOrNull<MeshInstance3D>(ShadowNodeName);
        if (shadow == null)
        {
            shadow = new MeshInstance3D
            {
                Name = ShadowNodeName,
                RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            unit.AddChild(shadow);
        }

        int footprintW = Mathf.Max(unit.FootprintW, 1);
        int footprintH = Mathf.Max(unit.FootprintH, 1);
        shadow.Mesh = new QuadMesh
        {
            Size = new Vector2(70.0f * footprintW, 48.0f * footprintH),
        };
        shadow.Position = new Vector3(
            0.0f,
            CombatLayerHeight.ContactShadowLayer - CombatLayerHeight.CharacterLayer,
            0.0f);

        if (shadow.MaterialOverride is not ShaderMaterial material || material.Shader != _shadowShader)
        {
            material = new ShaderMaterial
            {
                Shader = _shadowShader,
                RenderPriority = 1,
            };
            shadow.MaterialOverride = material;
        }

        material.SetShaderParameter("shadow_color", new Color(0.03f, 0.025f, 0.018f, 0.42f));
    }
}
