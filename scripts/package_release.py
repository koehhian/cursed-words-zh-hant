from pathlib import Path
import json,hashlib,subprocess,zipfile,shutil
root=Path(__file__).resolve().parents[1]; cfg=json.loads((root/'release.json').read_text(encoding='utf8'));out=root/'dist/release';out.mkdir(parents=True,exist_ok=True)
exe=root/cfg['installer'];assert hashlib.sha256(exe.read_bytes()).hexdigest()==cfg['sha256']
def add(z,name,data):
 i=zipfile.ZipInfo(name,(2026,11,7,0,0,0));i.compress_type=zipfile.ZIP_DEFLATED;i.external_attr=0o100644<<16;z.writestr(i,data)
shutil.copy2(exe,out/exe.name)
tracked=subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0');tracked=sorted(n for n in tracked if n)
win=out/('cursed-words-zh-hant-'+cfg['tag']+'-windows.zip')
with zipfile.ZipFile(win,'w') as z:
 add(z,exe.name,exe.read_bytes())
 for n in tracked:
  if n.startswith('docs/') or n in ['README.md','RELEASE_NOTES.md','LICENSE','THIRD_PARTY_NOTICES.md','ECDICT-LICENSE.txt','release-validation.txt']:add(z,n,(root/n).read_bytes())
source=out/('cursed-words-zh-hant-'+cfg['tag']+'-source.zip')
with zipfile.ZipFile(source,'w') as z:
 for n in tracked:
  if not n.startswith('distribution/'):add(z,n,(root/n).read_bytes())
assets=[out/exe.name,win,source]
(out/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in assets),encoding='utf8')
for p in assets:
 if p.suffix=='.zip':
  with zipfile.ZipFile(p) as z:
   assert z.testzip() is None
   assert not any(n.endswith('Assembly-CSharp.dll') or n.startswith('backup/') for n in z.namelist())
 print(p.name,p.stat().st_size)
