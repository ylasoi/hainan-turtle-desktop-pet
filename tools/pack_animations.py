"""Package generated sprite sheets into reusable animation assets (Pillow required)."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from PIL import Image, ImageDraw, ImageFont, ImageSequence

ROOT = Path(__file__).resolve().parents[1]
ANIMATIONS = {
    'walk': ('爬行', 140),
    'idle': ('呼吸眨眼', 300),
    'wave': ('抬手互动', 160),
    'sleep': ('缩壳打盹', 300),
    'mischief': ('回头搞怪', 180),
}
CANVAS = 384


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
        frames = []
        for index in range(8):
            frame = sheet.crop((round((index % 4) * sheet.width / 4), round((index // 4) * sheet.height / 2),
                                round((index % 4 + 1) * sheet.width / 4), round((index // 4 + 1) * sheet.height / 2)))
            # Runtime packing only: preserve the generated art and its alpha.
            frame = frame.resize((CANVAS, CANVAS), Image.Resampling.LANCZOS)
            bbox = frame.getchannel('A').point(lambda value: 255 if value > 24 else 0).getbbox()
            assert bbox and bbox[2] - bbox[0] > CANVAS * .25, (name, index, 'empty frame')
            assert frame.getpixel((0, 0))[3] < 12, (name, index, 'nontransparent corner')
            frame.save(frame_folder / f'{index:03d}.png')
            atlas_index = animation_index * 8 + index
            atlas.paste(frame, ((atlas_index % 4) * CANVAS, (atlas_index // 4) * CANVAS))
            frames.append(frame)
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
