import pathlib,gzip,hashlib,json,shutil
root=pathlib.Path(__file__).resolve().parent
package=root.parent/'payload'
release=root.parent
output=release.parent
build=root/'installer-resources'
build.mkdir(exist_ok=True)
files={'assembly':package/'Assembly-CSharp.dll','helper':package/'CursedWordsZh.dll','translations':package/'translations.tsv','dictionary':package/'dictionary.tsv','license':release/'ECDICT-LICENSE.txt'}
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
from binary_delta import create_delta
patched=sha(package/"Assembly-CSharp.dll")
original_bytes=(release/"backup/Assembly-CSharp.dll").read_bytes()
entries={}
for name,file in files.items():
    data=create_delta(original_bytes,file.read_bytes()) if name=='assembly' else file.read_bytes()
    (build/(name+'.gz')).write_bytes(gzip.compress(data,compresslevel=9,mtime=0))
    entries[name]=hashlib.sha256(data).hexdigest().upper()
original=sha(release/'backup/Assembly-CSharp.dll')
code='using System.Collections.Generic;\ninternal static class Build {\n'
code+='public const string Original="'+original+'";\npublic const string Patched="'+patched+'";\n'
code+='public static readonly Dictionary<string,string> Files = new Dictionary<string,string> {\n'
code+=',\n'.join('{"'+k+'","'+v+'"}' for k,v in entries.items())+'\n};\n}\n'
(build/'GeneratedBuild.cs').write_text(code,encoding='utf-8-sig')
manifest={'version':'1.0','gameVersion':'4.12','originalAssembly':original,'patchedAssembly':patched,'payloadSha256':entries}
(release/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
payload=release/'payload';payload.mkdir(exist_ok=True)
