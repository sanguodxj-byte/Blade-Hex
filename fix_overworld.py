#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import sys

filepath = 'src/scenes/overworld/OverworldScene.gd'

with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

# Step 1: Add is_waiting after is_time_paused at the top level
old_line = 'var is_time_paused: bool = false\n'
new_line = 'var is_time_paused: bool = false\nvar is_waiting: bool = false # 骑砍式等待模式\n'

# Only replace first occurrence (top level)
content = content.replace(old_line, new_line, 1)

# Step 2: Remove the incorrect is_waiting inside _setup_time_gradient
# Find "var is_waiting: bool = false # 骑砍式等待模式\n" and remove it
# We need to be careful since we just added one. Use rfind to find the last one.
target = 'var is_waiting: bool = false # 骑砍式等待模式\n'
idx = content.rfind(target)
if idx != -1:
    content = content[:idx] + content[idx + len(target):]
    print("Removed incorrect is_waiting definition")
else:
    print("Could not find incorrect is_waiting line")

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)

print("Fix applied successfully")
