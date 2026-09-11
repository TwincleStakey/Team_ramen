"""DialogueScenarioGenerator.cs 를 그대로 옮긴 시뮬레이터. 사용: python Tools/dialogue_sim.py [횟수] [시드]. JSON을 고치면 다시 돌려 0건인지 본다."""
import json, random, re, sys
from collections import Counter, defaultdict

N = int(sys.argv[1]) if len(sys.argv) > 1 else 300000
SEED = int(sys.argv[2]) if len(sys.argv) > 2 else 1
random.seed(SEED)
D = json.load(open(__file__.rsplit('Tools',1)[0] + 'Assets/Resources/DialogueDB.json', encoding='utf-8-sig'))

BASE = {
 'Shio':     {'ShioTare':1,'Broth':1,'ThinNoodles':1,'Chashu':1,'Menma':2,'GreenOnion':1,'FlavorOil':1},
 'Shoyu':    {'ShoyuTare':1,'Broth':1,'ThinNoodles':1,'Chashu':2,'Menma':1,'GreenOnion':1,'FlavorOil':1},
 'Tonkotsu': {'TonkotsuBase':1,'Broth':1,'ThickNoodles':1,'Chashu':1,'Egg':1,'Nori':1,'BeanSprout':1,'WoodEar':1,'GreenOnion':1,'FlavorOil':1},
}
KEYS = ["give","make","want","order","good","crave","ask","remove","less"]
personas = D['personas']; ramens = {r['ramenType']: r for r in D['ramens']}
by_p = defaultdict(list)
for o in D['openers']: by_p[('o', o['personaId'])].append(o['text'])
for c in D['closers']: by_p[('c', c['personaId'])].append(c['text'])
kw = defaultdict(list)
for k in D['keywords']: kw[(k['type'], k['enumName'])].append(k['value'])
conflict_rows = [(k['enumName'], k['value']) for k in D['keywords'] if k['type']=='Conflict']

def exact_count(t):
    return False

def find_t(ing, amt, diff):
    return [r['template'] for r in D['templates'] if r['ingredient']==ing and r['amount']==amt and r['difficulty']==diff]

def random_template(ing, amt, diff):
    ex = find_t(ing, amt, diff) + find_t('Any', amt, diff)
    ex = [t for t in ex if not exact_count(t)]
    if ex: return random.choice(ex)
    fb = [r['template'] for r in D['templates'] if r['ingredient'] in (ing,'Any') and r['amount']==amt and not exact_count(r['template'])]
    if not fb:
        return {-1:"{ing} {remove}.", -2:"{ing} {less}.", -3:"면은 {ing}으로 바꿔서 {give}."}.get(amt, "{ing} {amt} {give}.")
    return random.choice(fb)

def random_keyword(t, e):
    v = kw.get((t, e)); return random.choice(v) if v else e

def speech(p, key, connecting):
    rows = p['connecting'] if connecting else p['terminal']
    for r in rows:
        if r['key']==key and r['values']: return random.choice(r['values'])
    if connecting: return speech(p, key, False)
    return ''

def amt_word(p, a):
    return random.choice(p['amt1'] if a<=1 else p['amt2'] if a==2 else p['amt3'])

def render(text, ramen, p, change, connecting):
    text = text.replace('{ramen}', random_keyword('Ramen', ramen)).replace('{ramen_desc}', random_keyword('RamenDesc', ramen))
    if change:
        ing = change['ing']
        text = text.replace('{ing}', random_keyword('Ingredient', ing)).replace('{ing_desc}', random_keyword('IngDesc', ing))
        text = text.replace('{amt}', amt_word(p, change['expr']))
        u = next((r for r in D['units'] if r['ingredient']==ing), None)
        text = text.replace('{unit}', '' if not u else (u['unit1'] if change['expr']<=1 else u['unit2'] if change['expr']==2 else u['unit3']))
    split = text.rfind('. ') if connecting else -1
    head, tail = (text[:split+2], text[split+2:]) if split >= 0 else ('', text)
    for k in KEYS:
        head = head.replace('{'+k+'}', speech(p, k, False)); tail = tail.replace('{'+k+'}', speech(p, k, connecting))
    text = head + tail
    text = text.replace('!.', '!').replace('?.', '?')
    text = text.replace('  ', ' ').strip()
    if connecting and text.endswith('.') and not text.endswith('..'): text = text[:-1] + ','
    return text

def generate():
    p = random.choice(personas); diff = random.randint(1,3)
    ramen = random.choice(['Shio','Shoyu','Tonkotsu']); base = BASE[ramen]
    hint = None
    if diff >= 2:
        c = [h for h in D['hints'] if h['ramenType']==ramen and h['difficulty']==diff and p['personaId'] not in (h['excludePersona'] or [])]
        hint = random.choice(c)['template'] if c else None
    conflicts = {e for e, v in conflict_rows if hint and v in hint}
    cands = [x for x in ramens[ramen]['addable'] if x not in conflicts]; random.shuffle(cands)
    swap = random.random() < 0.4
    maxc = min(4, len(cands) + (1 if swap else 0)); total = random.randint(1, maxc)
    slots = total-1 if swap else total
    changes = []; reqs = []
    if swap:
        dn, sn = ('ThickNoodles','ThinNoodles') if ramen=='Tonkotsu' else ('ThinNoodles','ThickNoodles')
        changes.append({'ing':sn,'kind':'Swap','delta':1,'expr':1}); reqs += [(dn,-1),(sn,1)]
    for ing in cands[:slots]:
        b = base.get(ing,0); roll = random.random()
        if b>0 and roll<0.2: ch={'ing':ing,'kind':'Remove','delta':-b,'expr':0}
        elif b>=2 and roll<0.35: ch={'ing':ing,'kind':'Less','delta':-1,'expr':1}
        else:
            d=random.randint(1,3); ch={'ing':ing,'kind':'Add','delta':d,'expr':d}
        changes.append(ch); reqs.append((ing, ch['delta']))
    target = dict(base)
    for ing, a in reqs: target[ing] = min(4, max(0, target.get(ing,0)+a))
    lines = [random.choice(by_p[('o',p['personaId'])])]
    if hint: lines.append(render(hint, ramen, p, None, True))
    lines.append(render(random_template('Ramen',0,diff), ramen, p, None, len(changes)>0))
    used = []
    for i, ch in enumerate(changes):
        code = {'Remove':-1,'Less':-2,'Swap':-3}.get(ch['kind'], ch['expr'])
        t = random_template(ch['ing'], code, diff)
        for _r in range(4):
            if t not in used: break
            t = random_template(ch['ing'], code, diff)
        used.append(t)
        conn = i < len(changes)-1 and random.random() < 0.55
        lines.append(render(t, ramen, p, ch, conn))
    if len(changes) < 3 and random.random() < 0.5:
        f = random.choice(D['fillers'])
        if not swap or '면' not in f: lines.append(render(f, ramen, p, None, False))
    lines.append(random.choice(by_p[('c',p['personaId'])]))
    return p['personaId'], diff, ramen, changes, target, lines, used, hint

# ---- 검사 규칙 ----
issues = Counter(); samples = defaultdict(list); tmpl_use = Counter(); over4 = Counter()
def flag(name, text):
    issues[name]+=1
    if len(samples[name])<4: samples[name].append(text)

for _ in range(N):
    pid, diff, ramen, changes, target, lines, used, hint = generate()
    for t in used: tmpl_use[t]+=1
    full = '\n'.join(lines)
    for ing, v in target.items():
        if ing not in ('ShioTare','ShoyuTare','TonkotsuBase','Broth','ThinNoodles','ThickNoodles') and v>4:
            over4[(ramen,ing,v)]+=1; flag('정답 5 이상', f'{ramen} {ing}={v}')
    if '면로' in full: flag('조사 오류(면로)', full)
    if re.search(r'(라면|라멘).*(주세요|해다오|주십시오)[!.]?$', lines[0]): flag('opener가 이미 주문', full)
    if hint and any(k in lines[2] for k in ('시오 라멘','쇼유 라멘','돈코츠 라멘')): flag('힌트 뒤 라멘 직접 지명', full)
    if any(re.search(r'(고|는데|은데|니까|니께)\.$', l) for l in lines): flag('연결어미+마침표', full)
    if '라면' in full and '라멘' in full: flag('라멘/라면 혼용', full)
    if any(re.search(r'(고|는데|은데|고요|고예|(?<!되)구유|고잉)[!.] ', l) for l in lines): flag('문장 중간 연결어미', full)
    if len(used) != len(set(used)): flag('같은 뼈대 2회', full)
    if full.count('이번엔') > 1 or full.count('그냥') > 1: flag('이번엔/그냥 중복', full)
    if full.count('많을수록') > 1: flag('많을수록 중복', full)
    if re.search(r'(변경해|말아)(넣어|추가|곁들여|투하)', full): flag('변경해/말아+어미 결합', full)
    if re.search(r'[!?]\.', full): flag('문장부호 중복(!. ?.)', full)
    if re.search(r'\{\w+\}', full): flag('슬롯 미치환', full)
    if len(lines) != len(set(lines)): flag('같은 문장 반복', full)
    if '많이 많이' in full or '조금 조금' in full or '살짝 살짝' in full or '듬뿍 듬뿍' in full: flag('수량어 중복', full)
    if hint and any(w in hint for w in ('가볍게','기름진 건 피하고')) and any(c['ing']=='FlavorOil' and c['kind']=='Add' for c in changes): flag('힌트-요청 모순(기름)', full)
    if hint and '고기' in hint and any(c['ing']=='Chashu' and c['kind']=='Add' for c in changes): flag('힌트-요청 모순(고기)', full)
    if hint and any(w in hint for w in ('담백','자극','깔끔')) and any(c['ing']=='ChiliPowder' and c['kind']=='Add' for c in changes): flag('힌트-요청 모순(매움)', full)
    if any('짜지 않게' in l or '따뜻하게' in l or '단단하게' in l for l in lines): flag('조절 불가 필러 노출', full)
    if any((('조금만' in l and '더' not in l) or '있으면' in l) for l, c in zip(lines[2+(1 if hint else 0):], changes) if c['kind']=='Add'): flag('추가인데 감소로 읽힘', full)
    if any('두 개는 꼭' in l for l in lines): flag('계란 두 개는 꼭(추가2)', full)
    if any(l.endswith(',') or l.endswith('고') or l.endswith('고요') for l in lines[-1:]): flag('마지막 줄 연결형', full)
    if len(lines) > 8: flag('9줄 이상', full)
    if any(len(l) > 40 for l in lines): flag('40자 초과 줄', max(lines, key=len))

print(f'생성 {N}회 (seed {SEED})\n')
for k, v in issues.most_common(): print(f'{v:>8}  {v/N*100:6.2f}%  {k}')
print('\n-- 정답 5 이상 상세'); [print(' ', k, v) for k, v in sorted(over4.items(), key=lambda x:-x[1])[:10]]
print('\n-- 한 번도 안 쓰인 템플릿')
for r in D['templates']:
    if r['ingredient']!='Ramen' and tmpl_use[r['template']]==0: print(' ', r['ingredient'], r['amount'], r['difficulty'], r['template'])
print('\n-- 예시')
for k in issues:
    print(f'\n[{k}]'); [print('  '+s.replace('\n','\n  ')+'\n') for s in samples[k][:2]]
