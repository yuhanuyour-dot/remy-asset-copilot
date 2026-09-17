"""Stage redistributable files only; never include user data or development caches."""
from pathlib import Path
import argparse, shutil, json, hashlib
parser=argparse.ArgumentParser()
parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parent.parent)
parser.add_argument('--output',type=Path)
parser.add_argument('--runtime',type=Path)
a=parser.parse_args(); root=a.root.resolve(); out=(a.output or root/'work/installer-payload').resolve()
if out.exists(): raise SystemExit(f'Staging target already exists: {out}. Use a new --output directory.')
out.mkdir(parents=True)
runtime=(a.runtime or root/'runtime').resolve()

def copy(src,dst):
    dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(src,dst)

source=root/'AssetCopilot/dist-universal'
for f in source.rglob('*'):
    if f.is_file() and f.suffix.lower() not in ('.pdb',) and f.name != 'AssetCopilot.dll':
        copy(f,out/'AssetCopilot/dist'/f.relative_to(source))
for rel in ['AssetCopilot/sample/demo-chair.glb','AssetCopilot/THIRD-PARTY-NOTICES.md',
            'Start-AssetCopilot.cmd','Start-AssetCopilot.ps1','Start-AssetCopilot.vbs','Install-Update.ps1',
            'runtime/node.exe','runtime/NODE-LICENSE.txt']:
    source_file = runtime/Path(rel).relative_to('runtime') if rel.startswith('runtime/') else root/rel
    if rel == 'Start-AssetCopilot.ps1': source_file=root/'installer/launcher/Start-AssetCopilot.ps1'
    copy(source_file,out/rel)
# Only remove native binaries for other operating systems/architectures.
# Preserve all licenses, dependency metadata, JS dependencies and Windows x64 libraries.
vision=runtime/'vision'
for f in vision.rglob('*'):
    if not f.is_file(): continue
    rel=f.relative_to(vision); path=rel.as_posix()
    if path in ('model/onnx/text_model_quantized.onnx','model/onnx/vision_model_quantized.onnx'): continue
    if '/bin/napi-v3/' in path and '/bin/napi-v3/win32/x64/' not in path: continue
    if '/cache/' in '/'+path or '__pycache__' in rel.parts: continue
    copy(f,out/'runtime/vision'/rel)
copy(root/'installer/使用说明.txt',out/'使用说明.txt')
manifest=[]
for f in sorted(out.rglob('*')):
    if f.is_file(): manifest.append({'path':f.relative_to(out).as_posix(),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
(out.parent/(out.name+'-manifest.json')).write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(f'Staged {len(manifest)} files, {sum(x["bytes"] for x in manifest)/1024**2:.1f} MiB: {out}')
