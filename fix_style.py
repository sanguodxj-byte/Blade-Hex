p = r'D:\123\gen_missing_sprites.py'
content = open(p, 'r', encoding='utf-8').read()
content = content.replace(
    'Game sprite, single item on plain white background, clear hard outline, sharp edges, 2D hand-painted style, warm tones, simple flat shading. Item centered in frame. NOT 3D, NOT photorealistic.',
    'Hand-painted 2D game sprite, transparent background, 1px dark outline, ink-brown shadows, gouache texture, FFTactics style, dark medieval fantasy, NOT cartoon, NOT bright.'
)
content = content.replace(
    'Top-down hex tile texture for strategy game, seamless edges, hand-painted style, earthy natural tones, no objects just terrain surface. 256x256 pixels.',
    'Top-down hex tile, dark medieval fantasy, seamless, gouache, low saturation earthy, NOT bright, NOT cartoon.'
)
open(p, 'w', encoding='utf-8').write(content)
print('Style updated')
