"""Inventory C# literals for review without mistaking them for translated UI."""
import hashlib,json,re,gzip
from pathlib import Path
files=[]
for path in sorted(Path('.').rglob('*.cs')):
    if any(part in {'bin','obj','.git'} for part in path.parts):continue
    source=path.read_text(encoding='utf-8-sig')
    entries=[]
    for match in re.finditer(r'"""[\s\S]*?"""|@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*"',source):
        text=match.group()
        entries.append({'id':hashlib.sha256((str(path)+text).encode()).hexdigest()[:20], 'line':source.count('\n',0,match.start())+1,'source':text,'status':'review-context'})
    files.append({'file':str(path),'sha256':hashlib.sha256(source.encode()).hexdigest(),'entries':entries})
output=Path('translation-workspace');output.mkdir(exist_ok=True)
(output/'server-inventory.json').write_text(json.dumps({'fileCount':len(files),'literalCount':sum(len(f['entries']) for f in files),'files':files},ensure_ascii=False)+'\n')
(output/"server-inventory.json.gz").write_bytes(gzip.compress((output/"server-inventory.json").read_bytes(), mtime=0))
print(f"Inventoried {len(files)} C# files, {sum(len(f['entries']) for f in files)} literals; classification pending.")
