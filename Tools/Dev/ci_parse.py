import sys,re,base64,os
p,out=sys.argv[1],sys.argv[2]
s=open(p).read().replace('\\n','\n')
for l in s.split('\n'):
    if 'CI TEST FAILED' in l or 'CI TESTS DONE' in l or 'error CS' in l: print(l[:400])
i=0
while True:
    i=s.find('CI TEST FAILED',i)
    if i<0: break
    seg=s[i:i+600].split('\n')
    print('\n'.join(x[29:] for x in seg[1:5])); i+=10
shots={}
for m in re.finditer(r'SHOT (\S+) (\d+) (\S+)', s):
    shots.setdefault(m.group(1),{})[int(m.group(2))]=m.group(3)
for k,v in shots.items():
    try: open(os.path.join(out,k+'.jpg'),'wb').write(base64.b64decode(''.join(v[i] for i in sorted(v))))
    except Exception as e: print('bad',k,e)
print('shots',len(shots))
