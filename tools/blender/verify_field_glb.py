import json
import struct
import sys
from pathlib import Path

path = Path(sys.argv[1])
raw = path.read_bytes()
magic, version, total = struct.unpack_from('<III', raw)
assert magic == 0x46546C67 and version == 2 and total == len(raw)
length, kind = struct.unpack_from('<II', raw, 12)
assert kind == 0x4E4F534A
doc = json.loads(raw[20:20+length])
assert doc.get('meshes') and doc.get('nodes')
for accessor in doc.get('accessors', []):
    assert accessor['count'] > 0
names = [node.get('name', '') for node in doc['nodes']]
for prefix in sys.argv[2:]:
    assert any(name.startswith(prefix) for name in names), prefix
print(f"GLB QA PASS {path.name}: meshes={len(doc['meshes'])}, animations={len(doc.get('animations', []))}")
