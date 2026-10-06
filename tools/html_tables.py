import sys, re, json
from html.parser import HTMLParser
class T(HTMLParser):
    def __init__(s): super().__init__(); s.tables=[]; s.stack=[]; s.row=None; s.cell=None
    def handle_starttag(s,t,a):
        if t=='table': s.stack.append([])
        elif t=='tr' and s.stack: s.row=[]
        elif t in('td','th') and s.row is not None: s.cell=''
        elif t=='br' and s.cell is not None: s.cell+=' / '
    def handle_endtag(s,t):
        if t=='table' and s.stack:
            tb=s.stack.pop()
            if tb: s.tables.append(tb)
        elif t=='tr' and s.row is not None and s.stack: s.stack[-1].append(s.row); s.row=None
        elif t in('td','th') and s.cell is not None and s.row is not None: s.row.append(re.sub(r'\s+',' ',s.cell).strip()); s.cell=None
    def handle_data(s,d):
        if s.cell is not None: s.cell+=d
def tables(fn):
    p=T(); p.feed(open(fn,encoding='utf-8',errors='ignore').read()); return p.tables
def text(fn):
    h=open(fn,encoding='utf-8',errors='ignore').read()
    h=re.sub(r'<(script|style)[^>]*>.*?</\1>',' ',h,flags=re.S)
    return re.sub(r'\s+',' ',re.sub(r'<[^>]+>',' ',h))
if __name__=='__main__':
    fn=sys.argv[1]; maxrows=int(sys.argv[2]) if len(sys.argv)>2 else 60
    for i,t in enumerate(tables(fn)):
        if len(t)<2: continue
        print(f"\n-- table {i} ({len(t)} rows x {max(len(r) for r in t)} cols)")
        for r in t[:maxrows]: print(' | '.join(r)[:400])
