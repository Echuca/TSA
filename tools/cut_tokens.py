# Cuts individual counters out of the scanned counter sheet into TSA/wwwroot/images/tokens.
import sys
from pathlib import Path
from PIL import Image, ImageDraw

sheet = Image.open(sys.argv[1]).convert('RGB')
out = Path(sys.argv[2]); out.mkdir(parents=True, exist_ok=True)
S = 40          # counter size on the sheet
PAD = 2         # trim the cut lines
OUT_SIZE = 64

def cut(name, x, y):
    tile = sheet.crop((round(x) + PAD, round(y) + PAD, round(x) + S - PAD, round(y) + S - PAD))
    tile.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS).save(out / f'{name}.png')
    return name

def grid(x0, y0, block, row, col):
    return x0 + 40.5 * col, y0 + 90 * block + 40 * row

US_FRONT, USSR_FRONT, USSR_BACK, US_BACK = (18, 27), (407, 27), (18, 638), (409, 638)
names = []

for kind, origin in [('us-control', US_FRONT), ('ussr-control', USSR_FRONT)]:
    for value, (b, r, c) in {1: (0, 0, 0), 2: (1, 1, 0), 3: (3, 0, 0), 4: (4, 0, 0)}.items():
        names.append(cut(f'{kind}-{value}', *grid(*origin, b, r, c)))

for kind, origin in [('ussr-influence', USSR_BACK), ('us-influence', US_BACK)]:
    for value, (b, r, c) in {1: (0, 0, 0), 2: (1, 1, 0), 3: (3, 0, 1), 4: (3, 0, 0)}.items():
        names.append(cut(f'{kind}-{value}', *grid(*origin, b, r, c)))

specials = {
    'us-control-5': (598, 519), 'us-control-6': (543, 519), 'us-control-7': (502, 519), 'us-control-8': (502, 559),
    'ussr-control-5': (692, 477), 'ussr-control-6': (692, 517), 'ussr-control-7': (692, 558), 'ussr-control-8': (732, 558),
    'ussr-influence-5': (18, 1088), 'ussr-influence-6': (18, 1128), 'ussr-influence-7': (58, 1169), 'ussr-influence-8': (18, 1169),
    'us-influence-5': (112, 1128), 'us-influence-6': (207, 1128), 'us-influence-7': (247, 1128), 'us-influence-8': (247, 1169),
    'vp': (17, 477), 'turn': (57, 477), 'defcon': (17, 519),
    'milops-us': (112, 477), 'milops-ussr': (112, 559),
    'space-ussr': (112, 519), 'space-us': (153, 519),
    'ar-ussr': (153, 559), 'ar-us': (598, 1169),
    'restricted': (692, 1128),
}
events = {
    16: (206, 477), 17: (247, 477), 42: (301, 477), 43: (342, 477),
    21: (206, 517), 23: (247, 517), 44: (301, 517), 55: (342, 517),
    27: (206, 558), 35: (247, 558), 59: (301, 558), 65: (342, 558),
    68: (407, 477), 73: (448, 477), 96: (502, 477), 97: (543, 477),
    82: (407, 517), 83: (448, 517), 86: (407, 558), 87: (448, 558),
}
for name, (x, y) in specials.items():
    names.append(cut(name, x, y))
for card, (x, y) in events.items():
    names.append(cut(f'event-{card}', x, y))

# Contact sheet for checking the cuts.
cols = 10
contact = Image.new('RGB', (cols * 110, ((len(names) + cols - 1) // cols) * 90), 'white')
d = ImageDraw.Draw(contact)
for i, n in enumerate(names):
    x, y = (i % cols) * 110, (i // cols) * 90
    contact.paste(Image.open(out / f'{n}.png'), (x + 20, y))
    d.text((x + 2, y + 68), n, fill='black')
contact.save(sys.argv[3])
print(len(names), 'tokens')
