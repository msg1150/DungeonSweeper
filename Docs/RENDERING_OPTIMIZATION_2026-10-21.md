# 2026-10-21 던전 그리기 요청 최적화

## 확인한 구조

몬스터 4마리 조우 시 약 780 Draw Calls라는 사용자 측정값을 바탕으로 렌더링 코드를 확인했다. 해당 프레임 캡처를 직접 분석하거나 플레이를 재현하지는 않았다.

기존 감지 범위 디버그 표시는 원 하나를 64개 선분으로 나누고, 각 선분마다 `GUIUtility.RotateAroundPivot`와 `GUI.DrawTexture`를 호출했다. 몬스터 한 마리의 기본 인식 64개·청취 점선 32개·은신 발각 64개를 합하면 최대 160개 요청이다. 4마리면 640개, 모두 경계 중이면 확장 인식 256개가 더해져 896개까지 생긴다. 화면 밖 선분은 제외하므로 실제 요청 수는 위치에 따라 달라진다. 이 수는 코드상의 그리기 요청 수이며 GPU Draw Calls 실측값과 동일하다고 단정하지 않는다.

미니맵은 탐색한 보이는 칸과 표식을 각각 IMGUI 텍스처로 그렸다. 한편 2.5D 던전 바닥·벽·장식은 `ThreeDWorldView.BuildDungeon`에서 재질별로 메시를 합치고 있었으므로 이번에는 변경하지 않았다. 오브젝트 풀은 생성·파괴 비용을 줄이는 기능이며 이 그리기 요청 문제와는 별개다.

## 변경

- 모든 몬스터의 범위 선을 `MonsterPerceptionLines`의 UI 메시로 묶었다. 원별 색·투명도는 정점 색으로 저장하고 기본 공유 UI 재질을 사용한다. 실선·점선·월드 거리·소음 후 확장 범위·카메라 투영을 유지한다. 4마리의 범위 선은 경계 상태까지 포함해 한 Graphic에 들어가며 텍스트 라벨은 기존 IMGUI를 사용한다.
- 선은 카메라와 시야 합성이 갱신된 후 계산한다. 시야 오버레이 위·HUD 아래의 Screen Space Overlay Canvas를 사용하며 클릭을 받지 않는다. 최대 16,000선분·64,000정점마다 다음 Graphic으로 나눠 대량 스폰 시 uGUI 정점 한도를 넘지 않게 한다. 목록과 Graphic은 이후 프레임에 재사용한다.
- 기존 `Show Monster Perception Ranges` 체크로 전환한다. 꺼졌거나 HUD가 숨겨진 상태에는 선 메시 생성·투영·갱신과 라벨 생성을 건너뛰고 Canvas를 숨긴다. 관련 표시는 계속 `UNITY_EDITOR` 조건 안에 있어 게임 빌드에서 제외한다. 체크 상태를 임의로 변경하지 않았다.
- 미니맵 바닥·계단·은신처를 한 장의 `DungeonMinimapTexture`에 합쳤다. 탐색한 칸 수·도면·배율이 바뀔 때만 픽셀과 텍스처를 갱신한다. 플레이어 표식과 스크롤은 따로 그려 이동만으로는 텍스처를 다시 업로드하지 않는다. 지도 내용은 `GUI.DrawTexture` 한 번으로 제출하고 기존 고정 뷰포트에서 잘라낸다.
- 미탐색 칸과 은신처를 노출하지 않으며 저장 복원·층 전환·확대/축소 때 캐시를 새로 만든다. 비활성화·층 종료 시 소유 텍스처를 해제한다. 최대 배율에서 긴 축이 약 630픽셀인 캐시를 사용하며 칸 경계는 픽셀 단위로 정렬되므로 기존 벡터 사각형과 가장자리 표현에 약간 차이가 있을 수 있다.

## 검증 범위

격리 프로젝트의 `Tools/Validate-GameFlow.ps1 -EditorOnly`에서 기존 검사 포함 **114개**가 통과했다. C# 컴파일 오류·경고와 검사 예외는 없었다. 새 검사는 선분의 정점·색·두께와 uGUI 한도, 프레임 사이 정리, 미니맵 좌표·표식·미탐색 정보, 불필요한 조회/갱신 생략, 층·배율 변경과 텍스처 해제를 확인한다. Play 모드·게임 빌드·플레이어 실행·실제 프레임 프로파일링은 하지 않았다.

### 범위 선 미표시 보완

첫 구현은 선분 데이터 검사만 통과했지만 실제 화면에서 수치만 보이고 선이 보이지 않았다. 정상 Graphic 재빌드 경로를 검사하니 커스텀 Graphic에 `CanvasRenderer`가 없어 화면으로 메시를 제출하지 못했다. `MonsterPerceptionLines`에 필수 구성요소 특성을 추가해 런타임 생성 시 CanvasRenderer와 RectTransform을 함께 붙인다. Overlay Canvas도 일반 HUD의 자식에서 최상위 오브젝트로 분리하고 HUD 종료 핸들러가 명시적으로 정리하도록 바꿨다. 최상위 배치 기준은 [Unity Canvas 안내](https://docs.unity.cn/6000.0/Documentation/Manual/class-Canvas.html)를 따른다.

추가 검사는 제작 코드의 Canvas·선 Graphic 생성 경로를 사용해 최상위 계층, 정상 `Graphic.Rebuild`에서 CanvasRenderer로 정점·인덱스·재질이 전달되는 것, 정리 핸들러가 별도 Canvas를 제거하는 것을 Edit 모드에서 확인한다. 화면의 실제 가시성은 수동 플레이에서 확인한다. 수정 전 Play 세션에서 만들어진 오브젝트에는 필수 구성요소 특성이 소급 적용되지 않으므로 Play를 종료 후 다시 시작해야 한다.

`Assets/MidtermProfiling/`와 기존 프로파일링 결과·활성화 방식은 유지했다. `Assets/_Recovery/`의 사용자 복구 씬도 변경하지 않았다.

## 사용자가 비교할 항목

1. 같은 해상도·던전·카메라 위치·미니맵 배율·탐색 상태에서 몬스터 4마리가 보이는 장면을 측정한다. 정지 또는 Pause로 장면을 맞추면 비교하기 쉽다.
2. 감지 범위 디버그를 켠 상태와 끈 상태에서 각각 Draw Calls·Batches·SetPass Calls를 기록한다. Frame Debugger는 비교하는 Game 카메라/대상을 동일하게 선택한다. 에디터 전체 창을 합산한 수치와 게임 카메라 수치를 섞지 않는다.
3. 디버그를 켜면 `Batched perception debug / Perception lines 0`의 CanvasRenderer가 선을 묶어서 그리는지 확인한다. 기존의 선분별 회전 텍스처 요청은 없어야 한다. 소음으로 빨간 확장 원을 추가하고, 표시를 끄거나 메뉴를 열었을 때 선이 남지 않는지도 확인한다.
4. 미니맵이 플레이어를 따라가고 `[ / ]` 배율을 유지하는지, 계단·은신처·미탐색 영역이 올바르게 표시되는지, 층 이동·이어하기 후 이전 층 표식이 남지 않는지 확인한다.
5. 새 구역을 발견하는 순간의 CPU 시간·GC.Alloc도 확인한다. 캐시는 반복 그리기를 줄이는 대신 탐색·배율 변경 시 텍스처를 갱신한다. 최종 Draw Calls와 FPS 변화는 실제 측정 결과로 판단한다.

측정 기준은 [Unity Draw Call 최적화 안내](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/reduce-draw-calls/optimizing-draw-calls-choose-method)와 [Frame Debugger 안내](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/profile-rendering/frame-debugger/debug)를 참고한다.
