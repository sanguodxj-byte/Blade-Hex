"""
Fix all GDScript-style dynamic Call() invocations to AudioManager.
Convert from: audio?.Call("play_sfx_name", "ui_click")
To:           audio?.PlaySfxName("ui_click")
"""
import os
import re

root = r'D:\123\Blade&Hex\BladeHexFrontend\src'

# Map of GDScript snake_case method names to C# PascalCase
METHOD_MAP = {
    'play_sfx_name': 'PlaySfxName',
    'play_sfx_name_random_pitch': 'PlaySfxNameRandomPitch',
    'play_scenario_bgm': 'PlayScenarioBgm',
    'play_attack_hit_sfx': 'PlayAttackHitSfx',
    'play_attack_miss_sfx': 'PlayAttackMissSfx',
    'play_spell_cast_sfx': 'PlaySpellCastSfx',
    'play_spell_impact_sfx': 'PlaySpellImpactSfx',
}

# Pattern: varname.Call("method_name", arg1, arg2, ...)
# or varname?.Call("method_name", arg1, arg2, ...)
call_pattern = re.compile(
    r'(\w+)(\??)\.Call\("([^"]+)"(?:,\s*(.+?))?\)'
)

total_fixes = 0

for dp, dn, fns in os.walk(root):
    for f in fns:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dp, f)
        with open(path, 'r', encoding='utf-8', errors='replace') as fh:
            content = fh.read()
        
        original = content
        
        def replace_call(m):
            global total_fixes
            var_name = m.group(1)
            null_cond = m.group(2)  # ? or empty
            method_name = m.group(3)
            args = m.group(4)  # may be None
            
            if method_name not in METHOD_MAP:
                return m.group(0)  # Don't touch unknown methods
            
            cs_method = METHOD_MAP[method_name]
            total_fixes += 1
            
            if args:
                # Fix float literal issues: 2.0 -> 2.0f for float params
                # PlayScenarioBgm takes (int scenario, string variant, float crossfadeTime)
                # PlaySfxName takes (string sfxName, float volumeDb, float pitchScale)
                return f'{var_name}{null_cond}.{cs_method}({args})'
            else:
                return f'{var_name}{null_cond}.{cs_method}()'
        
        content = call_pattern.sub(replace_call, content)
        
        if content != original:
            # Also fix the type of audio variable from Node to AudioManager where possible
            # Pattern: GetNodeOrNull<Node>("/root/AudioManager") -> GetNodeOrNull<AudioManager>("/root/AudioManager")
            content = content.replace(
                'GetNodeOrNull<Node>("/root/AudioManager")',
                'GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager")'
            )
            # Pattern: GetNodeOrNull("/root/AudioManager") -> GetNodeOrNull<AudioManager>("/root/AudioManager")
            # But only when followed by ?.PlaySfx or similar
            content = re.sub(
                r'GetNodeOrNull\("/root/AudioManager"\)',
                'GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager")',
                content
            )
            
            # Add using if needed
            if 'BladeHex.Audio' not in content and 'AudioManager' in content:
                # Check if there's already a using block
                if 'using Godot;' in content:
                    content = content.replace(
                        'using Godot;',
                        'using Godot;\nusing BladeHex.Audio;',
                        1
                    )
            
            with open(path, 'w', encoding='utf-8', newline='') as fh:
                fh.write(content)
            print(f"  Fixed: {os.path.relpath(path, root)}")

print(f"\nTotal Call() conversions: {total_fixes}")
