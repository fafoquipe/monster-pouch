"""Read-only audit of the Android package and its packed scenery textures."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'Library/MonsterPouchApkInspection'))
import UnityPy

SHEETS = ['park-pieces', 'brief-classic-cabinet-v2', 'brief-lounge-cabinet-v2',
          'brief-royal-cabinet-v2', 'casino-accessories-props', 'royal-props', 'furniture-props']

def inspect_atlas(raw):
    textures = []
    for obj in UnityPy.load(raw).objects:
        if obj.type.name != 'Texture2D':
            continue
        data = obj.read()
        texture = dict(name=data.m_Name, width=data.m_Width, height=data.m_Height,
                       format=int(data.m_TextureFormat), filterMode=int(data.m_TextureSettings.m_FilterMode),
                       mipCount=data.m_MipCount, readable=data.m_IsReadable)
        assert texture['format'] == 47, texture
        assert texture['filterMode'] == 0, texture
        assert texture['mipCount'] == 1 and not texture['readable'], texture
        textures.append(texture)
    assert textures, 'Missing packed texture'
    return dict(sha256=hashlib.sha256(raw).hexdigest(), textures=textures)

parser = argparse.ArgumentParser()
parser.add_argument('--apk', type=Path)
parser.add_argument('--baseline', type=Path)
parser.add_argument('--all-scenery-atlases', action='store_true')
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
folder = ROOT / 'Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/assets/bin/Data'
atlases = {}
for sheet in SHEETS if args.all_scenery_atlases else SHEETS[:1]:
    meta = (ROOT / 'Assets/art/ambientations/modular' / (sheet + '.spriteatlasv2.meta')).read_text(encoding='utf-8-sig')
    guid = re.search(r'^guid: ([a-f0-9]+)$', meta, re.MULTILINE).group(1)
    atlases[sheet] = dict(guid=guid, **inspect_atlas((folder / guid).read_bytes()))
result = dict(gradleAtlases=atlases)
if args.apk:
    with zipfile.ZipFile(args.apk) as archive:
        bad = archive.testzip()
        assert bad is None, bad
        apk_atlases = {}
        for sheet, atlas in atlases.items():
            packed = inspect_atlas(archive.read('assets/bin/Data/' + atlas['guid']))
            assert packed['sha256'] == atlas['sha256'], sheet
            apk_atlases[sheet] = packed
        abis = sorted({name.split('/')[1] for name in archive.namelist() if name.startswith('lib/') and name.endswith('.so')})
        assert abis == ['arm64-v8a', 'armeabi-v7a'], abis
    with args.apk.open('rb') as file:
        digest = hashlib.file_digest(file, 'sha256').hexdigest()
    result['apk'] = dict(path=str(args.apk), bytes=args.apk.stat().st_size,
                         sha256=digest, crcValid=True, architectures=abis, atlases=apk_atlases)
    if args.baseline:
        baseline_size = args.baseline.stat().st_size
        result['baseline'] = dict(path=str(args.baseline), bytes=baseline_size)
        result['differenceBytes'] = result['apk']['bytes'] - baseline_size
        result['differencePercent'] = round(result['differenceBytes'] / baseline_size * 100, 2)
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))

