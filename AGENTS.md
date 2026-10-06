# 프로젝트 작업 지침

## 프로파일링 파일 유지

2026-10-07 사용자 요청: 프로파일링 관련 파일은 현재 유지하고, 사용자가 추후 명시적으로 비활성화 또는 삭제를 요청할 때 해당 작업을 수행합니다.

- 대상 소스: `Assets/MidtermProfiling/` 전체, 폴더 및 파일의 `.meta`, Runtime/Editor `.asmdef`.
- 주요 파일: `Runtime/MidtermProfilingLab.cs`, `Runtime/MidtermLootWorkload.cs`, `Editor/MidtermProfilingWindow.cs`.
- 측정 결과 경로: `.utmp/MidtermProfiling/`, `ProfilerCaptures/` (생성된 경우). Git에서 무시되어도 이 요청 없이 정리 대상으로 삭제하지 않습니다.
- 출시 준비, 코드 정리, 최적화를 이유로 이 도구를 임의로 삭제하거나 비활성화하지 않습니다. 기존 컴파일 조건과 활성화 방식도 유지합니다.
- `PlayerVisualProfile`과 렌더링 `VolumeProfile`은 성능 측정 도구가 아니므로 프로파일링 삭제 요청의 대상으로 혼동하지 않습니다.
