import pathlib,json,base64
s=pathlib.Path(__file__).resolve().parent
r=json.loads((s/"translations.json").read_text(encoding="utf8"))
with (s.parent/"payload/translations.tsv").open("w",encoding="utf8",newline="\n") as f:
    for a,b in r.items(): f.write(base64.b64encode(a.encode()).decode()+"\t"+base64.b64encode(b.encode()).decode()+"\n")
