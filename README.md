# Dungeon Sweeper

몬스터와의 전투 대신 **탐색, 시체 해체, 전리품 회수와 탈출**에 집중하는 2D 던전 게임입니다. 적의 순찰과 추적을 피하면서 회수품을 제한된 가방에 배치하고, 살아서 마을로 돌아와 수익을 확보합니다.

현재는 메인 메뉴부터 마을·던전·저장/불러오기까지 플레이할 수 있는 **개발 중인 프로토타입**입니다. 최종 이미지·영상·음악을 연결할 구조를 마련했으며, 콘텐츠와 출시 검증은 계속 진행 중입니다.

| 항목 | 현재 기준 |
| --- | --- |
| 엔진 | Unity **6000.6.0f1** |
| 렌더링 / 입력 | Universal Render Pipeline / Input System |
| 대상 플랫폼 | Windows 64비트, 오프라인 싱글 플레이 |
| 시작 씬 | `MainMenu → Town → Dungeon` |
| 저장 | 자동 저장 1개 + 수동 저장 10개, 수동 슬롯 수 변경 가능 |

## 플레이 흐름

1. **마을에서 준비** — 길드에서 회수 의뢰를 받고 상점에서 해체 보급 도구를 구매합니다.
2. **던전 탐색** — 세 가지 레이아웃 중 하나를 탐색하며 몬스터의 순찰·추적·공격을 피합니다. 탐색한 구역은 미니맵에 남습니다.
3. **시체 해체** — 타이밍에 맞춰 해체를 진행합니다. 실패하면 시체가 손상되며, 보급 도구로 성공 횟수를 보충할 수 있습니다.
4. **전리품 정리** — 형태가 다른 회수품을 **5 × 4 가방**에 드래그해 배치하고, 회전하거나 포기합니다.
5. **탈출과 정산** — 입구 또는 특수 게이트로 탈출하면 회수 가치와 의뢰 보상을 받습니다. 사망하면 해당 던전의 미정산 전리품과 진행 중인 의뢰를 잃습니다.

## 구현된 기능

- **메인 메뉴**: 새 게임 → 이어하기 → 옵션 → 게임종료.
- **새 게임**: 진행도를 초기화하고 마을에서 시작합니다. 기존 수동 저장 파일은 유지합니다.
- **이어하기 / 수동 저장**: 슬롯 목록에서 저장을 선택하고, 수동 저장 시 원하는 슬롯에 기록합니다. 기존 슬롯에는 덮어쓰기 확인이 표시됩니다.
- **일시정지**: 게임 중 메뉴를 열거나 창이 포커스를 잃으면 플레이를 일시정지합니다.
- **옵션**: 전체·음악·효과음 음량, 전체화면/창 모드, 해상도, 품질, 수직 동기화, 프레임 제한. 변경은 적용 버튼으로 확정합니다.
- **던전 복원**: 배치, 플레이어 위치·체력·대시 상태, 탐색 지도, 가방, 시체·해체 진행, 미배치 전리품과 적 상태를 복구합니다.
- **저장 보호**: Windows 사용자별 암호화, 저장 내용 검증, 슬롯 바꿔치기 거부와 정상 백업 복구.
- **화면 대응**: 메뉴와 HUD를 창 크기에 맞춰 축소하고, 많은 전리품은 스크롤 목록으로 표시합니다.

## 실행하기

Unity Hub에서 **6000.6.0f1**과 Windows Build Support를 설치한 뒤 프로젝트를 여세요. 패키지와 에셋의 초기 임포트가 완료되면 [MainMenu 씬](Assets/Scenes/MainMenu.unity)을 열고 **Play**를 누릅니다.

```powershell
git clone https://github.com/msg1150/DungeonSweeper.git
```

`Town`과 `Dungeon` 씬을 직접 열어 플레이 테스트할 수도 있습니다. 메뉴부터 시작하는 전체 흐름은 `MainMenu`에서 확인하세요.

### 조작

| 입력 | 동작 |
| --- | --- |
| WASD | 이동 |
| Space | 대시 |
| E | 마을 상호작용, 해체 타이밍 입력, 탈출·결과 화면에서 귀환 |
| R | 해체 중 보급 도구 사용 |
| Esc | 해체 취소 또는 일시정지 메뉴 |
| 마우스 | 메뉴 선택, 전리품 드래그·회전·포기, 목록 스크롤 |

해체·전리품 정리 중에는 오른쪽 아래 **메뉴** 버튼으로 일시정지와 저장 화면을 엽니다. `Esc`는 해체 중 작업 취소로 동작합니다.

## 저장과 불러오기

자동 저장은 새 게임 시작, 던전 입장·귀환, 의뢰/보급 도구 변경, 해체 결과·전리품 정리, 던전 탈출/사망, 메인 화면 복귀와 정상 종료 시 갱신됩니다. 여러 요청은 같은 프레임의 최종 상태로 모아 저장합니다. 수동 슬롯은 플레이어가 직접 저장할 때만 바뀝니다.

저장은 `Application.persistentDataPath/Saves`에 `autosave.sav`, `slot-01.sav` 등의 암호화된 파일로 기록됩니다. 주 저장본에 문제가 있으면 검증을 통과한 `.bak` 백업을 불러옵니다. 쓰기가 실패하면 화면에 오류를 표시하고 자동 저장은 5초 뒤 재시도합니다. 복귀·종료 저장이 실패하면 현재 게임을 유지합니다. 강제 종료나 전원 차단 직전의 상태까지 보장하지는 않습니다.

**현재 저장 보호는 Windows DPAPI를 사용합니다.** 일반적으로 같은 PC와 Windows 계정에서 불러올 수 있으며, 저장 파일만 다른 PC로 복사하거나 Windows를 재설치하면 복구되지 않을 수 있습니다. 외부 도구를 통한 메모리·실행 파일 변경까지 차단하는 구조는 아닙니다. 상세 동작과 이전 정책은 [저장 보호 문서](Docs/SAVE_PROTECTION.md)를 참고하세요.

수동 슬롯 수는 [GameFlowConfig](Assets/Resources/GameFlowConfig.asset)의 **Manual Save Slot Count**에서 변경합니다. 슬롯 수를 줄여도 기존 파일을 삭제하지 않으며, 다시 늘리면 표시합니다. 옵션은 진행도와 분리해 PlayerPrefs에 저장합니다.

## 설정과 콘텐츠 추가

| 에셋 | 수정할 내용 |
| --- | --- |
| [GameFlowConfig](Assets/Resources/GameFlowConfig.asset) | 씬 이름, 수동 저장 슬롯 수, 상호작용 거리 |
| [DungeonTuning](Assets/Resources/DungeonTuning.asset) | 이동·대시, 체력, 시야, 몬스터 탐지·이동, 전리품 가치 배율 |
| [MonsterDatabase](Assets/Resources/MonsterDatabase.asset) | 몬스터 종류, 공격, 스프라이트, 드롭 확률·가격·형태 |
| [PlayerVisualProfile](Assets/Resources/PlayerVisualProfile.asset) | 플레이어 스프라이트와 애니메이션 |
| [MainMenuPresentation](Assets/Resources/MainMenuPresentation.asset) | 메인 배경·로고·영상·글꼴, 씬별 음악, 버튼 효과음, 믹서 출력 |

`MainMenuPresentation`의 **Background Image / Logo / Background Video**에 에셋을 지정하면 메뉴에 표시됩니다. 영상이 준비되기 전이나 재생에 실패하면 배경 이미지로 돌아갑니다. 영상 자체의 음성은 사용하지 않습니다.

**Menu / Town / Dungeon Music**은 씬별 반복 음악이며 **Button Sound**는 메뉴 효과음입니다. 새 효과음은 `GameAudio.PlayEffect`로 재생하면 효과음 음량 설정을 따릅니다. 미디어가 비어 있어도 기본 메뉴는 동작합니다. 배포용 한글 글꼴은 **UI Font**에 지정하세요. 현재 비어 있으면 OS 글꼴을 사용합니다.

새로운 특별 이벤트는 상태 변경을 완료한 뒤 아래 API를 호출해 완료 기록과 자동 저장을 요청할 수 있습니다.

```csharp
GameSession.CompleteEvent("unique_event_id");
```

## Windows 빌드

Unity 메뉴의 **Dungeon Sweeper → Build Windows Release**를 실행합니다. 결과는 `Builds/Windows/DungeonSweeper.exe`에 생성되며, 배포할 때는 해당 폴더의 데이터와 런타임 파일도 함께 제공해야 합니다.

빌드 전에 시작 씬 순서, 필수 씬·리소스·컴포넌트, 몬스터·드롭·밸런스 설정을 검사합니다. 누락이나 유효하지 않은 설정은 오류로 보고하고 빌드를 중단합니다. 현재 보호 제공자가 없는 다른 OS의 빌드도 차단합니다. 위 메뉴는 비개발 Windows 64비트 빌드이며, 스크립팅 백엔드는 프로젝트의 현재 설정을 사용합니다.

## 개발 검증

PowerShell에서 다음 명령을 실행할 수 있습니다.

```powershell
# Unity에서 전체 게임 흐름과 저장/로직 검사
./Tools/Validate-GameFlow.ps1

# 비개발 Windows Mono 플레이어 빌드·실행 및 프로세스 재시작 검사
./Tools/Validate-GameFlow.ps1 -CheckPlayerProtection

# 실제 그래픽 출력과 1280×720 / 640×360 화면 촬영 포함
./Tools/Validate-GameFlow.ps1 -CheckPlayerProtection -CaptureScreens
```

Unity가 기본 설치 경로에 없으면 `-UnityEditorPath "C:/path/to/Unity.exe"`를 지정합니다. 실행 검사는 Unity 라이선스와 해당 버전의 Windows 빌드 모듈이 필요합니다. 화면 촬영은 그래픽 출력을 사용할 수 있는 Windows 세션에서 실행하세요.

검사는 `.utmp/UnityValidation`에 만든 복사본에서 실행하고, 저장과 옵션을 테스트용으로 분리합니다. 테스트 드라이버는 `Tests`에 두고 복사본에만 주입하여 실제 배포 코드에 포함하지 않습니다. 로그·결과·촬영 화면도 이 폴더에서 확인할 수 있습니다.

| 위치 | 역할 |
| --- | --- |
| `Assets/Scripts/Demo` | 마을·던전 플레이, 메뉴, 저장, 옵션, 화면·음향 |
| `Assets/Scripts/Player` | 플레이어 이동과 대시 |
| `Assets/Scripts/Dismantling`, `Interaction` | 해체와 상호작용 |
| `Assets/Resources` | 설정 에셋, 셰이더, 스프라이트 |
| `Assets/Scenes` | 시작·플레이·개발 테스트 씬 |
| `Assets/Editor` | 설정 도구와 빌드 검사 |
| `Tests`, `Tools` | 격리된 실행 검증 코드와 PowerShell 실행 도구 |

## 출시 전 남은 작업

실행 검사를 통과한 프로토타입이며, 완성된 출시 버전을 의미하지 않습니다. 최종 미디어·글꼴 연결과 사용 권한 확인, 콘텐츠·난이도 조정, 실제 조작과 음향 재생, 여러 PC·해상도에서의 장시간 플레이, 저장 이전·클라우드 정책 확정이 필요합니다. 출시 검토에서 수정한 항목과 검증 범위는 [출시 준비 점검 기록](Docs/RELEASE_READINESS.md)에 정리했습니다.
