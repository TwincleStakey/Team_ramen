# -*- coding: utf-8 -*-
"""라멘 아트에셋 폴더를 Assets/Art/조리/ 로 정리한다. 한 번만 돌리면 되는 스크립트."""
from PIL import Image
import os, shutil, glob

SRC = 'Assets/Art/라멘 아트에셋/'
DST = 'Assets/Art/조리/'
ETC = 'Assets/Art/나머지/'
UNUSED = 'Assets/Art/안씀/'

os.makedirs(DST, exist_ok=True)

def move(src, dst):
    """png 와 meta 를 같이 옮긴다. meta 를 두고 가면 유니티가 GUID 를 새로 매겨 참조가 끊긴다."""
    if not os.path.exists(src):
        print('   없음:', src); return
    shutil.move(src, dst)
    if os.path.exists(src + '.meta'):
        shutil.move(src + '.meta', dst + '.meta')
    print('   %s → %s' % (os.path.basename(src), os.path.basename(dst)))

print('[1] 새 에셋 이름 정리')
RENAME = [
    ('시오통.png',                '타래_시오.png'),
    ('쇼유통.png',                '타래_쇼유.png'),
    ('돈코츠통.png',              '타래_돈코츠.png'),
    ('향미유통.png',              '향미유통.png'),
    ('시오&향미유 부을때.png',      '국자_시오향미유.png'),
    ('쇼유 부을때.png',            '국자_쇼유.png'),
    ('돈코츠 부울때.png',          '국자_돈코츠.png'),
    ('면통상시루프.png',           '면통.png'),
    ('면통_오른쪽제거.png',        '면통_왼쪽만.png'),
    ('오른쪽 물 삭제버전.png',      '면통_오른쪽만.png'),
    ('굵은면.png',                '면붓기_굵은면.png'),
    ('얇은면.png',                '면붓기_얇은면.png'),
]
for a, b in RENAME:
    move(SRC + a, DST + b)

move(SRC + '굵은면 물기털기/noodles_thick_sheet_1024x640.png', DST + '면털기_굵은면.png')
move(SRC + '얇은면 물기털기/noodles_thin_sheet_1024x640.png',  DST + '면털기_얇은면.png')

print('[2] 육수 낱장 4장 → 512x128 시트 한 장')
sheet = Image.new('RGBA', (512, 128), (0, 0, 0, 0))
for i in range(4):
    f = SRC + '육수팔팔/broth_bubbling_frame_%02d_128.png' % (i + 1)
    sheet.paste(Image.open(f).convert('RGBA'), (i * 128, 0))
sheet.save(DST + '육수 냄비.png')
print('   육수 냄비.png 512x128 (4프레임)')

print('[3] 배경 교체 (옛것은 git 에 남아 있다)')
shutil.copy(SRC + '제조화면_배경_벽면좁아진ver.png', 'Assets/Art/화면/제조화면 배경.png')
print('   제조화면 배경.png 덮어씀')

print('[4] 시트와 똑같은 낱장 84장 버림')
n = 0
for pat in ['굵은면 물기털기', '얇은면 물기털기', '육수팔팔']:
    for f in glob.glob(SRC + pat + '/*'):
        os.remove(f); n += 1
    d = SRC + pat
    if os.path.isdir(d): shutil.rmtree(d)
    if os.path.exists(d + '.meta'): os.remove(d + '.meta')
print('   %d개 삭제' % n)

print('[5] 아무도 안 읽는 옛 그림을 안씀/ 으로')
os.makedirs(UNUSED, exist_ok=True)
DEAD = ['국자.png', '국자 시오.png', '국자 쇼유.png', '국자 돈코츠.png', '국자 육수.png',
        '시오.png', '쇼유.png', '돈코츠.png',
        '시오 애니메이션.png', '쇼유 애니메이션.png', '돈코츠 애니메이션.png',
        '향미유.png', '젓가락.png', '번개.png', '손님_01-teen-male.png']
for n2 in DEAD:
    move(ETC + n2, UNUSED + n2)

print('[6] 남은 원본 폴더')
left = [f for f in glob.glob(SRC + '*') if not f.endswith('.meta')]
for f in left: print('   ', os.path.basename(f))
