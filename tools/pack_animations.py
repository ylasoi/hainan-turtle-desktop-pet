"""Package generated sprite sheets into reusable animation assets (Pillow required)."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image, ImageDraw, ImageFont, ImageSequence, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ANIMATIONS = {
    'walk': ('爬行', 140),
    'idle': ('呼吸眨眼', 300),
    'wave': ('抬手互动', 160),
    'sleep': ('完整缩壳', 300),
    'mischief': ('回头搞怪', 180),
}
CANVAS = 384


def extract_characters(sheet, erosion=3):
    """Extract the 8 separated silhouettes, including limbs crossing a nominal cell.

    Generated art need not use pixel-perfect dividers. Connected silhouettes keep
    a neighboring frame's hand/foot out of the next frame; no art is repainted.
    """
    mw, mh = max(1, sheet.width // 4), max(1, sheet.height // 4)
    small_alpha = sheet.getchannel('A').resize((mw, mh), Image.Resampling.BOX).filter(ImageFilter.MinFilter(erosion))
    pixels = small_alpha.tobytes()
    visited = bytearray(mw * mh)
    components = []
    for start in range(len(pixels)):
        if visited[start] or pixels[start] < 40:
            continue
        visited[start] = 1
        stack, members = [start], []
        left, top, right, bottom = mw, mh, 0, 0
        while stack:
            position = stack.pop()
            members.append(position)
            y, x = divmod(position, mw)
            left, top, right, bottom = min(left, x), min(top, y), max(right, x), max(bottom, y)
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < mw and 0 <= ny < mh:
                    neighbor = ny * mw + nx
                    if not visited[neighbor] and pixels[neighbor] >= 40:
                        visited[neighbor] = 1
                        stack.append(neighbor)
        if len(members) > 200:
            components.append((members, (left, top, right + 1, bottom + 1)))
    components.sort(key=lambda item: len(item[0]), reverse=True)
    if len(components) < 8 and erosion < 9:
        return extract_characters(sheet, erosion + 2)
    assert len(components) >= 8, f'Expected eight separated character silhouettes; found {len(components)}'
    selected = {}
    sx, sy = sheet.width / mw, sheet.height / mh
    cw, ch = sheet.width / 4, sheet.height / 2
    for members, small_box in components[:8]:
        box = (max(0, int(small_box[0] * sx) - 16), max(0, int(small_box[1] * sy) - 16),
               min(sheet.width, int(small_box[2] * sx) + 16), min(sheet.height, int(small_box[3] * sy) + 16))
        column = max(0, min(3, int(((box[0] + box[2]) / 2) / cw)))
        row = max(0, min(1, int(((box[1] + box[3]) / 2) / ch)))
        index = row * 4 + column
        assert index not in selected, 'Two characters assigned to the same animation cell'
        data = bytearray(mw * mh)
        for member in members:
            data[member] = 255
        mask = Image.frombytes('L', (mw, mh), bytes(data)).filter(ImageFilter.MaxFilter(2 * erosion + 3))
        mask = mask.resize(sheet.size, Image.Resampling.NEAREST).crop(box)
        character = sheet.crop(box)
        alpha = character.getchannel('A')
        alpha.paste(0, mask=mask.point(lambda value: 255 - value))
        character.putalpha(alpha)
        selected[index] = (character, (box[0] - column * cw, box[1] - row * ch,
                                        box[2] - column * cw, box[3] - row * ch))
    assert len(selected) == 8
    left = min(item[1][0] for item in selected.values())
    top = min(item[1][1] for item in selected.values())
    width = max(item[1][2] for item in selected.values()) - left
    height = max(item[1][3] for item in selected.values()) - top
    scale = (CANVAS - 32) / max(width, height)
    frames = []
    for index in range(8):
        character, bounds = selected[index]
        target = character.resize((max(1, round(character.width * scale)), max(1, round(character.height * scale))), Image.Resampling.LANCZOS)
        frame = Image.new('RGBA', (CANVAS, CANVAS))
        x = round(16 + (CANVAS - 32 - width * scale) / 2 + (bounds[0] - left) * scale)
        y = round(16 + (CANVAS - 32 - height * scale) / 2 + (bounds[1] - top) * scale)
        frame.alpha_composite(target, (x, y))
        frames.append(frame)
    return frames


def transparent_gif_frame(frame):
    # GIF supports one-bit transparency; PNG/APNG/WebP retain the full alpha.
    quantized = frame.convert('RGB').quantize(colors=255)
    palette = [0, 0, 0] + quantized.getpalette()[:765]
    quantized = quantized.point(lambda value: value + 1)
    quantized.putpalette(palette)
    mask = frame.getchannel('A').point(lambda value: 255 if value < 128 else 0)
    quantized.paste(0, mask=mask)
    quantized.info['transparency'] = 0
    return quantized


def main():
    parser = argparse.ArgumentParser()
    for key in ANIMATIONS:
        parser.add_argument('--' + key, required=True, type=Path)
    args = parser.parse_args()
    assets = ROOT / 'assets'
    previews = ROOT / 'docs' / 'previews'
    previews.mkdir(parents=True, exist_ok=True)
    (assets / 'sheets').mkdir(parents=True, exist_ok=True)
    (assets / 'animations').mkdir(parents=True, exist_ok=True)
    atlas = Image.new('RGBA', (4 * CANVAS, 10 * CANVAS))
    manifest = {
        'schema_version': 1, 'canvas': [CANVAS, CANVAS],
        'anchor': [0.5, 0.92], 'atlas': 'sprites.png',
        'atlas_columns': 4, 'atlas_rows': 10, 'animations': {},
    }
    all_frames = {}
    for animation_index, (name, (label, duration)) in enumerate(ANIMATIONS.items()):
        source = getattr(args, name)
        destination = assets / 'sheets' / (name + '.png')
        if source.resolve() != destination.resolve():
            shutil.copyfile(source, destination)
        with Image.open(destination) as opened:
            sheet = opened.convert('RGBA')
        assert sheet.width >= 800 and sheet.height >= 400, (name, sheet.size)
        assert sheet.getchannel('A').getextrema() == (0, 255), name + ' must have a transparent background'
        frame_folder = assets / 'frames' / name
        frame_folder.mkdir(parents=True, exist_ok=True)
        frames = extract_characters(sheet)
        normalized_sheet = Image.new('RGBA', (4 * CANVAS, 2 * CANVAS))
        for index, frame in enumerate(frames):
            bbox = frame.getchannel('A').point(lambda value: 255 if value > 24 else 0).getbbox()
            assert bbox and bbox[2] - bbox[0] > CANVAS * .25, (name, index, 'empty frame')
            assert frame.getpixel((0, 0))[3] < 12, (name, index, 'nontransparent corner')
            frame.save(frame_folder / f'{index:03d}.png')
            atlas_index = animation_index * 8 + index
            atlas.paste(frame, ((atlas_index % 4) * CANVAS, (atlas_index // 4) * CANVAS))
            normalized_sheet.paste(frame, ((index % 4) * CANVAS, (index // 4) * CANVAS))
        normalized_sheet.save(destination)
        all_frames[name] = frames
        animation_folder = assets / 'animations'
        frames[0].save(animation_folder / (name + '.png'), save_all=True,
                       append_images=frames[1:], duration=duration, loop=0, disposal=2, blend=0)
        frames[0].save(animation_folder / (name + '.webp'), save_all=True,
                       append_images=frames[1:], duration=duration, loop=0, lossless=True, method=4)
        gif_frames = [transparent_gif_frame(frame) for frame in frames]
        gif_frames[0].save(animation_folder / (name + '.gif'), save_all=True,
                           append_images=gif_frames[1:], duration=duration, loop=0,
                           disposal=2, transparency=0, optimize=False)
        cream_frames = []
        for frame in frames:
            preview = Image.new('RGBA', frame.size, '#e9e7dd')
            preview.alpha_composite(frame)
            cream_frames.append(preview.convert('RGB'))
        cream_frames[0].save(previews / (name + '.gif'), save_all=True,
                             append_images=cream_frames[1:], duration=duration, loop=0)
        manifest['animations'][name] = {
            'label': label, 'frame_count': 8, 'duration_ms': duration, 'loop': True,
            'first_atlas_frame': animation_index * 8,
            'frames': [f'frames/{name}/{index:03d}.png' for index in range(8)],
            'sheet': f'sheets/{name}.png', 'sheet_columns': 4, 'sheet_rows': 2,
            'apng': f'animations/{name}.png', 'gif': f'animations/{name}.gif',
            'webp': f'animations/{name}.webp',
        }
        print(f'{name}: 8 frames, {duration} ms/frame, APNG + WebP + GIF validated')
    atlas.save(assets / 'sprites.png', optimize=True)
    # The default portrait and icon use the relaxed, grounded idle pose.
    all_frames['idle'][0].save(assets / 'character.png')
    all_frames['idle'][0].save(assets / 'pet.ico', format='ICO',
                               sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    (assets / 'animations.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    font_path = Path('C:/Windows/Fonts/msyh.ttc')
    font = ImageFont.truetype(str(font_path), 17) if font_path.exists() else ImageFont.load_default()
    overview_frames = []
    for frame_index in range(8):
        overview = Image.new('RGB', (960, 680), '#e9e7dd')
        draw = ImageDraw.Draw(overview)
        for action_index, (name, (label, duration)) in enumerate(ANIMATIONS.items()):
            x, y = (action_index % 3) * 320, (action_index // 3) * 340
            image = all_frames[name][frame_index].resize((300, 300), Image.Resampling.LANCZOS)
            overview.paste(image, (x + 10, y + 28), image)
            draw.text((x + 14, y + 6), label, font=font, fill='#354b37')
        draw.text((670, 420), '海南腿毛龟\n连续动作模板\n5 组 × 8 帧', font=font, fill='#354b37', spacing=14)
        overview_frames.append(overview)
    overview_frames[0].save(previews / 'all-actions.gif', save_all=True,
                             append_images=overview_frames[1:], duration=180, loop=0)
    overview_frames[0].save(previews / 'overview.png')

    # Verify the encoded animations, not just the pre-encoding frame objects.
    for name in ANIMATIONS:
        for extension in ('gif', 'png', 'webp'):
            path = assets / 'animations' / (name + '.' + extension)
            with Image.open(path) as animation:
                assert animation.n_frames == 8, (path, animation.n_frames)
                assert animation.info.get('loop') == 0, path
                for frame in ImageSequence.Iterator(animation):
                    assert frame.size == (CANVAS, CANVAS), path
    paths = sorted(assets.rglob('*.png')) + sorted(assets.rglob('*.gif')) + sorted(assets.rglob('*.webp'))
    checksums = [hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + path.relative_to(ROOT).as_posix() for path in paths]
    (ROOT / 'assets-sha256.txt').write_text('\n'.join(checksums) + '\n', encoding='utf-8')
    print('All 15 encoded loops passed frame-count, loop and canvas checks.')


if __name__ == '__main__':
    main()
