# DungeonSweeper 아트 적용 기록

2026-10-08. 이미지 제작에는 Codex 내장 imagegen을 사용했다. 기존 프로젝트 이미지와 이번에 생성한 캐릭터 시트를 스타일 참고로 사용했으며, 외부 작가·게임·브랜드의 이미지는 새로 가져오지 않았다.

## 공통 제작 기준

- 플레이어: 하얀 털, 귀만 연한 크림색, 초록 눈, 모자 없음. 민트 작업복·크림색 반팔·갈색 신발을 착용한 두 발로 걷는 고양이 청소부.
- 행동: 전투보다 빠른 회수와 도주. 메인 일러스트에는 밀대를 사용하고, 이동 중에는 도구를 허리에 보관한다.
- 마을: 밝은 아이보리 석재, 세이지 녹지, 민트 지붕, 살구색 거래소 천막.
- 던전: 어두운 회청색·청록색 석재. 게이트 주변에만 약한 민트빛. 게임의 시야 가림은 별도로 적용한다.
- UI: 크림색 패널·민트 버튼·어두운 글자. 기존 메뉴와 상호작용을 유지한다.
- 몬스터·시체: 선명한 실루엣과 손으로 그린 질감, 고어 없는 회수 대상. 고블린·슬라임·오크의 생체와 시체 정체성을 맞춘다.

## 적용 파일과 시트 순서

아래 경로의 기준은 프로젝트 루트다. 기존 PNG를 교체하면서 .meta GUID와 Resources 경로를 유지했다.

| 파일 | 구성 / 사용 |
| --- | --- |
| Assets/Art/MainMenu/MainMenuCatJanitor-v4.png | 긴박한 청소·도주와 어두운 던전의 현재 메인 화면 |
| Assets/Resources/Sprites/player.png | 별도 대기 자세 |
| Assets/Resources/Sprites/player-walk-cycle.png | 2×2 이동 4프레임, 좌상→우상→좌하→우하 |
| Assets/Resources/Sprites/player-dash-cycle.png | 2×2 대시 4프레임, 출발→질주→도약→착지 |
| Assets/Resources/Sprites/Monsters/goblin-sheet.png | 3×2, 위 이동 3프레임 / 아래 공격 3프레임 |
| Assets/Resources/Sprites/Monsters/slime-sheet.png | 위 점프 이동 / 아래 몸통 공격 |
| Assets/Resources/Sprites/Monsters/orc-sheet.png | 위 이동 / 아래 몽둥이 공격 |
| Assets/Resources/Sprites/Monsters/corpse-sheet.png | 3×1, 고블린→슬라임→오크 |
| Assets/Resources/Sprites/loot-icons.png | 위 이빨→단검→젤 / 아래 코어→가죽→뿔 |
| Assets/Resources/Sprites/Environment/town-casual.png | 건물이 따로 배치되는 밝은 마을 배경 |
| Assets/Resources/Sprites/Environment/town-structures.png | 2×2, 위 길드→거래소 / 아래 게이트→창고 |
| Assets/Resources/Sprites/Environment/dungeon-floor-casual.png | 어두운 석재 바닥 |
| Assets/Resources/Sprites/Environment/dungeon-wall-casual.png | 같은 계열의 어두운 석재 벽 |
| Assets/Resources/Sprites/escape-portal.png | 약하게 빛나는 탈출 게이트 |
| Assets/Resources/Sprites/enemy-goblin.png | 이전 단일 스프라이트 참조용, 새 고블린과 일치 |
| Assets/Resources/Sprites/corpse.png | 이전 단일 스프라이트 참조용, 새 고블린 시체와 일치 |
| Assets/Art/Generated/Backgrounds/town-background-16x9.png | 마을 배경의 장면/편집 참조용 동일 사본 |
| Assets/Art/Generated/Backgrounds/dungeon-background-16x9.png | 게이트 주변만 약하게 빛나는 던전 배경 참조 |
| Assets/Art/Generated/town-background.png | 이전 마을 배경 참조를 위한 동일 사본 |
| Assets/Art/Generated/dungeon-background.png | 이전 던전 배경 참조를 위한 동일 사본 |

메인 화면의 상세 편집 프롬프트는 [MAIN_MENU_ART.md](MAIN_MENU_ART.md)에 있다. v1~v3는 이전 비교안이며 현재 게임에는 v4만 연결한다.

## 재생성용 프롬프트 기준

다음은 에셋별 공통 제작 요청을 정리한 재생성 기준이다. 이미지 모델 재실행만으로 현재 결과의 픽셀·프레임 정렬을 동일하게 재현할 수는 없으므로, 현재 시트를 참고하고 Unity에서 다시 확인한다.

```text
Original hand-painted, outlined 2D dungeon-cleaning game art.
White anthropomorphic bipedal janitor cat, only ears faint pale cream/yellow,
green eyes, pink nose, no hat, mint work overalls, cream short sleeve shirt,
brown work shoes, white tail, cloth/tool pouch. Alert, hurried noncombatant.
Match the current main menu and player reference images.
Sprite cutouts: true transparent background, complete body, consistent scale,
clear padding per frame, no text, no watermark, no baked floor or rectangular backdrop.
Player walk: four right-facing running poses in a 2x2 equal-cell grid, top row first.
Player dash: launch, fast stride, airborne, landing in that same grid.
Monsters: 3x2 equal-cell grid, three walk/hop poses on top, three attack poses below.
Goblin: sage/olive gray skin, pointed ears, dark spiky hair, brown leather, worn dagger.
Slime: mint translucent jelly, hop and body-slam poses.
Orc: bulky olive skin, tusks, brown vest/trousers, wooden club.
Corpses: 3x1 goblin, collapsed mint slime, curled orc, shut eyes, no blood or gore.
Loot icons: 3x2 tooth, dagger, gel / core, hide, horn, no decorative tile backgrounds.
Town: bright ivory stone, sage greenery, soft teal stream, open building pads.
Buildings: 2x2 guild cottage, market awning / stone gate, warehouse and bag workshop.
Dungeon: moderately dark cool slate/teal stone paving and walls, readable game surfaces,
subtle moss, restrained light ONLY at the mint escape gate.
```

이동 시트의 실제 최종 편집 프롬프트:

```text
Edit the supplied 2x2 sprite sheet for Unity slicing. Preserve the exact white cat character, cream-tinted ears only, green eyes, mint janitor overalls, cream shirt, brown boots, white tail, pouch, no hat, hand-painted outline style and the SAME FOUR POSES in the SAME grid positions. Essential technical correction: four exact equal square cells on a square canvas, with a wide empty TRANSPARENT gutter along BOTH central grid lines. Shrink each figure within its OWN cell to at most 80% of cell width and 82% height. No figure, foot, paw, tail, outline, shadow, streak or detached pixel may touch or cross any cell boundary. Every cell must have at least 8% completely transparent padding on ALL FOUR sides. Align standing/landing baseline consistently within the cells. Clean up all detached specks and stray fragments. Fully transparent background, no visible frame, grid lines, labels, or text. Retain full limbs and tail entirely visible. Top row: running contact and recoil; bottom row: opposite running contact and passing stride. FOUR right-facing run poses, not crouching.
```

대시 시트의 실제 최종 편집 프롬프트:

```text
Edit the supplied 2x2 sprite sheet for Unity slicing. Preserve the exact white cat character, cream-tinted ears only, green eyes, mint janitor overalls, cream shirt, brown boots, white tail, pouch, no hat, hand-painted outline style and the SAME FOUR POSES in the SAME grid positions. Essential technical correction: four exact equal square cells on a square canvas, with a wide empty TRANSPARENT gutter along BOTH central grid lines. Shrink each figure within its OWN cell to at most 80% of cell width and 82% height. No figure, foot, paw, tail, outline, shadow, streak or detached pixel may touch or cross any cell boundary. Every cell must have at least 8% completely transparent padding on ALL FOUR sides. Align standing/landing baseline consistently within the cells. Clean up all detached specks and stray fragments. Fully transparent background, no visible frame, grid lines, labels, or text. Retain full limbs and tail entirely visible. Top left crouched launch, top right long fast stride, bottom left airborne stretched stride, bottom right low landing. Keep tiny subtle mint dash accents within each cell padding, not across cells.
```

마지막 단일 시체 이미지의 실제 최종 프롬프트:

```text
Create a single standalone 2D game sprite of ONLY the left goblin corpse in the supplied reference sprite sheet. Preserve exactly its sage olive skin, black spiky hair, long pointed ears, brown torn leather vest, charcoal trousers, brown shoes, small worn dagger beside its hand. The whole goblin lies collapsed sideways with eyes shut, full body visible, peaceful defeated lootable corpse, no blood or gore. Match the hand-painted outlined storybook dungeon-cleaning game style precisely. One sprite centered with generous transparent padding on every side, square canvas. No slime, no orc, no text, no ground or backdrop or frame. True transparent background.
```

## Unity 연결

캐릭터와 건물 시트는 원본 비율을 유지하도록 NPOT 변환을 끄고 최대 텍스처 크기를 4096으로 설정했다. 런타임 가공은 GPU 사본을 사용하므로 원본 Read/Write는 껐다. 대기 스프라이트의 피벗은 이동 시트와 동일한 (0.5, 0.35)다.

PlayerVisualAnimator는 정지하면 별도 대기 이미지를 표시하고, 이동 시 상단부터 4프레임을 순환한다. 실제 이동 대시 0.15초에는 준비·질주·도약을 재생하고, 이동이 끝나면 dashRecoverySeconds(현재 0.08초) 동안 착지를 표시한다. poseBlendSeconds(현재 0.045초)로 대시 자세와 착지→이동/대기 전환을 짧게 섞는다. 두 SpriteRenderer를 재사용하며 프레임마다 오브젝트를 생성하지 않는다. 저장에서 대시를 복원할 때도 실제 이동 타이머와 방향을 따른다. 이 시각 전환은 이동 속도·거리·쿨다운을 늘리지 않는다. 칸 왼쪽 안전 여백은 PlayerVisualProfile.frameLeftInset(현재 6%)로 제외해 이웃 프레임의 작은 조각을 숨긴다. 새 시트를 만들 때 이 여백에는 본체를 넣지 않는다. 피벗의 원래 위치는 유지하고, 새 Sprite는 플레이어 제거 시 해제한다. 물리 이동·충돌·몬스터 능력치·던전 스폰 수·전리품·저장 구조는 변경하지 않았다.

메인 메뉴의 카드 배경은 MainMenuPresentation.menuPanelOpacity(0.56), 버튼 배경은 menuButtonOpacity(0.8)로 조절한다. GUI.backgroundColor를 사용해 글자까지 반투명해지는 것을 피한다. 마을 상태창은 390×54, 상세 의뢰가 있으면 390×78로 줄이고 알파 0.64를 적용했다. 시설 이름표는 상호작용 거리 + 0.65 이내에 들어올 때만 작은 반투명 태그로 표시하며, 자세한 기능은 하단 E 안내에서 보여준다.

기본 고블린·오크의 타격 궤적은 중간 공격 칸의 오른쪽 경계를 넘어 회복 칸 왼쪽에 섞여 있었다. MonsterVisualAnimator는 타격 칸의 표시 영역을 넓혀 무기와 궤적 전체를 포함하고, 회복 칸에는 이웃 궤적만 제외하는 모서리 메시를 적용한다. 아랫쪽 오크 옷자락과 피벗은 보존한다. 가공 결과는 종류별로 캐시하고 사용자 제작 시트에는 이 보정을 적용하지 않는다. 메시 좌표와 UV 처리는 [Unity Sprite.OverrideGeometry 문서](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Sprite.OverrideGeometry.html)를 따른다.

TownCasualVisuals는 건물 시트를 2×2로 읽고 네 시설에 각각 연결한다. 창고도 임시 색 사각형 대신 건물 이미지를 표시한다. ConceptArtPreviewRefresh는 기본 몬스터·시체 미리보기의 크기가 달라졌을 때 기존 Sprite 에셋을 갱신하며 GUID를 보존한다. 필요하면 Unity 메뉴 Dungeon Sweeper → Refresh Default Art Previews로 다시 실행할 수 있다.

프로파일링 파일과 측정 결과는 유지한다. Assets/Art/Generated의 전체 던전 배경은 편집/참조 이미지이며, 실제 던전은 타일 바닥·벽과 시야 가림으로 구성한다.

## 검증

Tools/Validate-GameFlow.ps1 -CaptureScreens로 격리된 Unity 프로젝트와 일반 Windows 빌드를 확인한다. 실제 사용자 저장은 사용하지 않는다. 고양이 대기·이동·대시, 프레임 순서, 네 건물 매핑, 시트 크기, 투명 배경을 추가로 검사한다.

최종 검증 결과: Unity 런타임 검사 497개, 일반 Windows 빌드 검사 398개, 별도 프로세스 재실행 후 저장 복원 검사 10개 통과. 화면 16장의 파일 생성과 비어 있지 않은 픽셀을 확인하고, 메인 화면·마을·던전·아트/공격 갤러리를 직접 검토했다. 대시 종료 후 착지 유지·대기 복귀·알파 복원과 고블린·오크 공격 세 프레임의 좌우 방향, 전체 궤적과 피벗 보존을 추가로 검사한다. 기존 에셋의 GUID 변경과 프로파일링 소스 변경은 없다. 이 수치는 기능 검증이며 프레임 성능 측정 결과는 아니다.

반복 검사에서 해체 성공 구간이 매우 넓을 때 Random.Range의 부동소수 오차로 끝점이 막대를 아주 조금 넘는 사례도 확인했다. 시작 위치를 허용 범위로 제한하고 128회 반복 검사를 추가했다. 해체 난이도 값은 유지했다.

검증 캡처는 .utmp/UnityValidation/에 생성되며 Git에서 무시된다. release-art-gallery.png는 위 대기/이동, 중간 대시, 아래 생체/시체 쌍을 같은 화면에 표시한다. 이 갤러리는 검증 빌드에만 들어가며 본 게임에는 포함되지 않는다. 숨겨진 Windows 테스트 창의 초기 캡처를 위해 먼저 화면 크기를 바꾸고, 화면 검증에는 Direct3D 11을 사용한다. 본 프로젝트의 기본 그래픽 API는 변경하지 않았다. 비어 있는 PNG가 성공으로 처리되지 않도록 최소 파일 크기도 확인한다.
