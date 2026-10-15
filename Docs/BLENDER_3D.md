# Blender 3D 표시

> 현재 기본 플레이는 [2.5D 표시](2_5D_PRESENTATION.md)다. 아래 실제 3D 모델은 비교·재사용을 위한 실험 원본으로 보관하며 기본 플레이에는 표시하지 않는다. 아래 검증 수치는 3D 실험 당시의 기록이다.

2026-10-16. Blender 5.2.2에서 메시, Armature, 본 가중치와 애니메이션을 제작하고 FBX로 내보냈다. 캐릭터는 SkinnedMeshRenderer로 렌더링한다. 현재는 형태와 게임 연결을 잡은 로우폴리 1차 모델이며 이전 일러스트의 세부 묘사를 그대로 재현한 최종 모델은 아니다.

## 원본과 Unity 파일

- `ArtSource/Blender/`: CatJanitor, Goblin, Slime, Orc, Town, Portal의 `.blend` 원본.
- `Assets/Models/Blender/`: 같은 이름의 FBX. 일반 플레이에는 Blender가 필요하지 않다.
- `Assets/Prefabs/Characters/CatJanitor3D.prefab`: 플레이어 모델.
- `Assets/Prefabs/Monsters/3D/`: 몬스터 모델. 같은 리그의 Defeated 자세를 시체에도 사용한다.
- `Assets/Prefabs/Environment/`: 마을과 게이트 모델.
- `Assets/Models/Materials/`: 공유 URP 재질.
- `Assets/Resources/ThreeDPresentation.asset`: 모델과 카메라 설정.
- `Tools/Build-3DAssets.py`: 기본 메시에서 형태와 리그를 생성하는 제작 스크립트. 외부 모델·텍스처 다운로드는 없다.

`.blend`를 열고 리그를 선택한 뒤 Action Editor에서 `CatJanitor_Walk` 등의 Action을 고른다. Idle, Walk, Dash, Attack, Loot, Defeated 여섯 종류를 포함한다. 팔·다리·머리·꼬리에 본 가중치가 있고, 슬라임은 Root의 위치와 스케일로 움직인다.

## 적용과 좌표

MainMenu에서 진입하면 3D 표시를 자동으로 만든다. 메인 일러스트와 UI·전리품 아이콘은 기존 2D 아트를 사용한다. 키보드 조작과 저장 형식은 유지한다.

기존 XY 논리 좌표를 XZ 지면으로 표시한다. `ThreeDWorldView.Map(x,y)`는 `(x,0,y)`다. Rigidbody2D, 경로 탐색, 상호작용 거리, 저장 위치를 유지하면서 화면·캐릭터를 실제 3D로 전환했다. 현재 게임에는 높이 이동이 없으며, 추후 점프나 층 이동을 추가하려면 별도 높이·3D 충돌 설계가 필요하다.

카메라는 레이어 30을 원근 투영으로 그린다. 기존 스프라이트는 논리 객체에 남지만 3D 화면에는 보이지 않는다. FBX 축 변환·루트 애니메이션은 내부 자식이, 외부 앵커는 게임 위치·회전이 담당한다. FaceOrigin/FaceForward 마커로 실제 정면을 판별해 이동·공격 방향을 맞춘다. 공격 클립은 기존 판정 시간과 같은 정규화 진행도를 사용한다.

밝은 마을과 어두운 던전을 조명·그림자로 구분하고, 게이트에 민트빛을 준다. 마을 건물 발판과 섬 경계에는 논리 충돌체를 둔다. 던전 벽 충돌은 기존 레이아웃을 사용한다. 바닥·벽·벽 윗면은 세 개의 합쳐진 메시로 만든다. 앞쪽 벽은 낮춰 플레이어가 가려지는 것을 줄였다. 시야 가림은 원근 카메라의 광선과 지면 교차점으로 계산한다.

## 풀과 자원

몬스터·시체의 3D 모델도 DontDestroyOnLoad 풀 아래 보관한다. 귀환하면 비활성화하고 다음 입장에 같은 인스턴스를 재사용한다. 보관 한도 초과 또는 메인 메뉴 캐시 정리 때 논리 배우와 모델을 함께 해제한다. 플레이어·장면 모델은 장면 수명을 따르고 런타임 메시·재질도 장면 종료 시 해제한다. FBX 메시와 재질은 공유한다.

이 구조는 재생성 비용을 줄이기 위한 것이며 목표 기기의 성능 측정 결과를 의미하지 않는다. 기존 MidtermProfiling 파일과 측정 결과는 유지했다.

## 편집

개별 모델은 원본을 편집해 같은 이름의 FBX로 교체한다. 기존 `.meta`는 유지한다. Z-up Blender 원본을 Y-up, -Z forward FBX로 내보내고 Armature/Mesh/정면 마커 Empty를 포함하며 leaf bone은 끈다. 클립 이름의 `_Idle`, `_Walk` 같은 접미사는 유지한다. 현재 Unity 임포트는 Legacy 본 애니메이션을 사용한다.

MonsterPrefab.model3D, CorpsePrefab.model3D 또는 공통 설정의 모델을 바꾸면 새 프리팹을 사용할 수 있다. 상태명 접미사와 정면 마커를 맞춘다. 마커가 없는 모델은 Unity +Z를 정면으로 간주한다. 공통 설정의 Use 3D를 끄면 기존 2D 표시와 비교할 수 있다.

전체 기본 모델을 제작 스크립트로 재생성하는 명령:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python Tools/Build-3DAssets.py
Copy-Item -Path .utmp/Blender3DOutput/*.fbx -Destination Assets/Models/Blender -Force
```

이 명령은 `.blend` 원본도 기본 형상으로 다시 생성한다. 직접 편집한 원본은 별도 파일이나 Git 커밋으로 보관한 뒤 사용한다. `.blend`는 Assets 밖에 있어 Unity가 자동으로 Blender를 실행하지 않는다. Unity 메뉴 `Dungeon Sweeper/Create Missing Blender 3D Assets`는 누락된 연결을 생성하고 이미 지정한 모델·프리팹은 보존한다.

## 검증

격리 프로젝트와 일반 Windows 빌드에서 저장 보호·새 게임·이어하기·던전 배치·경제·풀 한도 검사를 유지했다. 추가로 실제 스키닝, 여섯 클립의 빌드 포함, Walk에서의 다리 본 회전, 공격 후 정면 마커 방향, 귀환/재입장 시 3D 모델 객체 재사용을 검사한다.

실제 빌드 캡처: `.utmp/UnityValidation/release-town.png`, `release-dungeon.png`, `release-art-gallery.png`, `release-attack-gallery.png`. 2D 모드에서는 기존 시트 검사를, 3D 모드에서는 모델·본·클립 검사를 사용한다.

최종 검증: Unity 게임 흐름 497개, Windows 빌드 436개, 별도 프로세스 재실행 저장 복원 10개 통과. 16장의 캡처가 비어 있지 않음을 확인하고 모델·마을·던전·공격 방향을 시각 검토했다. 실제 게임 화면의 사본은 `Docs/Images/3D/`에 보관한다. 프로파일링 소스 변경은 없다.
