import sys, re
from html.parser import HTMLParser
# table parser with img alt/title capture and rowspan/colspan expansion
class T(HTMLParser):
    def __init__(s): super().__init__(); s.tables=[]; s.stack=[]; s.row=None; s.cell=None; s.cattr=None
    def handle_starttag(s,t,a):
        a=dict(a)
        if t=='table': s.stack.append([])
        elif t=='tr' and s.stack: s.row=[]
        elif t in('td','th') and s.row is not None: s.cell=''; s.cattr=(int(a.get('rowspan',1) or 1), int(a.get('colspan',1) or 1))
        elif t=='br' and s.cell is not None: s.cell+=' / '
        elif t=='img' and s.cell is not None: s.cell+=' ['+(a.get('alt') or a.get('title') or '').replace('MHWilds-','').replace('.png','')+'] '
    def handle_endtag(s,t):
        if t=='table' and s.stack:
            tb=s.stack.pop()
            if tb: s.tables.append(expand(tb))
        elif t=='tr' and s.row is not None and s.stack: s.stack[-1].append(s.row); s.row=None
        elif t in('td','th') and s.cell is not None and s.row is not None: s.row.append((re.sub(r'\s+',' ',s.cell).strip(),)+s.cattr); s.cell=None
    def handle_data(s,d):
        if s.cell is not None: s.cell+=d
def expand(rows):
    grid=[]; pend={}  # (r,c)->text
    for ri,row in enumerate(rows):
        out=[]; ci=0; it=iter(row)
        cell=next(it,None)
        while cell is not None or (ri,ci) in pend:
            if (ri,ci) in pend: out.append(pend.pop((ri,ci))); ci+=1; continue
            txt,rs,cs=cell
            for k in range(cs):
                out.append(txt)
                for r in range(1,rs): pend[(ri+r,ci)]=txt
                ci+=1
            cell=next(it,None)
        grid.append(out)
    return grid
def tables(fn):
    p=T(); p.feed(open(fn,encoding='utf-8',errors='ignore').read()); return p.tables
def text(fn):
    h=open(fn,encoding='utf-8',errors='ignore').read()
    h=re.sub(r'<(script|style)[^>]*>.*?</\1>',' ',h,flags=re.S)
    h=re.sub(r'<img[^>]*alt="([^"]*)"[^>]*>',r' [\1] ',h)
    return re.sub(r'\s+',' ',re.sub(r'<[^>]+>',' ',h))
if __name__=='__main__':
    fn=sys.argv[1]; mode=sys.argv[2]
    if mode=='tables':
        idx=[int(x) for x in sys.argv[3].split(',')] if len(sys.argv)>3 else None
        for i,t in enumerate(tables(fn)):
            if idx and i not in idx: continue
            if len(t)<2: continue
            print(f"\n-- table {i} ({len(t)} rows)")
            for r in t[:80]: print(' | '.join(r)[:500])
    else:
        tx=text(fn); kw=sys.argv[3]; w=int(sys.argv[4]) if len(sys.argv)>4 else 400
        for m in list(re.finditer(kw,tx))[:6]:
            print('...',tx[max(0,m.start()-w):m.start()+w],'...\n')
