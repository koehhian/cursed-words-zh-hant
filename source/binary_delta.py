import struct
def create_delta(original, target):
    # Index 16-byte anchors every 8 bytes; output copy ranges and literal edits.
    index={}
    for i in range(0,len(original)-15,8):
        key=original[i:i+16]
        if key not in index:index[key]=i
    out=bytearray(b'CWD1'+struct.pack('<I',len(target)))
    i=0;literal=bytearray()
    def flush():
        if literal:
            out.extend(b'L'+struct.pack('<I',len(literal))+literal);literal.clear()
    while i<len(target):
        pos=index.get(target[i:i+16]) if i+16<=len(target) else None
        if pos is None:literal.append(target[i]);i+=1;continue
        end=16
        while pos+end<len(original) and i+end<len(target) and original[pos+end]==target[i+end]:end+=1
        flush();out.extend(b'C'+struct.pack('<II',pos,end));i+=end
    flush();out.extend(b'E');return bytes(out)
