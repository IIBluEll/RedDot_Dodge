# 배경음악 재생 목록

`InGame` 씬의 루트 `Background Music` 오브젝트에 전용 AudioSource와 `BackgroundMusicPlayer`를 연결했습니다. 메뉴·플레이·결과·Retry·메뉴 복귀에서 같은 재생 목록을 유지합니다. 음악 파일은 사용자가 인스펙터에 연결합니다.

## 인스펙터 연결

1. 변경된 `InGame` 씬을 불러오고 Hierarchy에서 `Background Music`을 선택합니다.
2. `BackgroundMusicPlayer > Tracks`에 원하는 곡 수만큼 항목을 추가합니다.
3. 각 항목의 `Clip`에 AudioClip을 드래그하고 `Title`에 표시할 곡명을 입력합니다. 제목을 비우면 AudioClip 이름을 사용합니다.
4. 인스펙터 목록 순서대로 재생합니다. 앱 실행 후 메인 메뉴가 처음 표시된 프레임을 마친 뒤 실제 시간 기준 2초를 기다리고 첫 곡을 시작합니다. `Start Delay Seconds`에서 대기 시간을 조정할 수 있습니다. `Loop Playlist` 기본값은 켜짐이며 마지막 곡 뒤에는 처음으로 돌아갑니다.

첫 재생 예약은 `InGameFlowController`가 메인 메뉴를 표시한 뒤 한 번만 요청합니다. 이후 Retry나 메인 메뉴 복귀에서는 대기를 다시 예약하거나 곡을 처음부터 재생하지 않습니다.

빈 항목과 연결되지 않은 Clip은 건너뜁니다. 로드가 필요한 Clip은 로드 완료를 기다린 뒤 재생하며, 로드에 실패한 Clip은 다음 후보로 넘어갑니다. 재생 가능한 곡이 없으면 대기음이나 알림 없이 정지합니다. 게임 일시정지 중에도 음악과 다음 곡 전환은 계속됩니다. 앱이 백그라운드에 있거나 포커스를 잃은 동안에는 재생과 곡 전환을 정지합니다.

## 곡 전환 시 로딩

현재 연결한 `system_overload.mp3`, `electric_stream.mp3`, `ai_fight.mp3`의 임포트 설정을 `Load Type = Streaming`, `Load In Background = 켜짐`으로 변경했습니다. `Preload Audio Data`는 꺼진 상태로 두고 재생기에서 준비 시점을 관리합니다. 새 음악을 추가할 때도 같은 설정을 적용합니다.

첫 곡은 메인 메뉴가 표시된 뒤 2초를 기다리는 동안 준비합니다. 이후에는 현재 곡이 시작될 때 다음 유효 곡의 `LoadAudioData()`를 미리 요청합니다. 다음 곡이 아직 로딩 중이면 완료를 기다리고, 준비된 곡은 전환 시 재사용합니다. 반복 재생에서는 마지막 곡을 재생하며 첫 곡을 준비합니다. 백그라운드 로딩이 꺼진 음원에는 재생 도중 선행 로딩을 요청하지 않습니다.

기존 설정은 대형 음악을 전환 순간 동기 로드·압축 해제할 수 있는 조합이었습니다. 이번 변경은 그 로딩 경로를 조정한 것이며, 실제 프레임 시간이나 끊김 개선 여부를 실행·측정한 결과는 아닙니다.

배경음악 자체의 기본 크기는 같은 오브젝트의 `AudioSource > Volume`에서 조정합니다. 현재 씬 기본값은 0.35입니다. 설정 화면의 BGM 슬라이더를 기본 음량에 곱하고, 0%이면 BGM만 음소거합니다. BGM은 `ignoreListenerVolume = true`로 전역 리스너 음량을 무시하므로 SFX 슬라이더를 변경해도 음악 크기는 바뀌지 않습니다. 효과음은 개별 AudioSource의 기본 음량을 사용하고 SFX 설정을 `AudioListener.volume`에 적용합니다. `Loop Playlist`를 끄면 마지막 유효 곡 뒤에 재생을 종료합니다. AudioSource의 개별 곡 Loop는 재생기에서 끕니다.

## 곡명 알림

`MusicToast_Canvas > SafeArea > Now Playing Toast`에 기존 버튼 배경·재생 아이콘·폰트로 작은 알림을 배치했습니다. Safe Area의 왼쪽 아래에서 들어와 곡명을 표시하고 다시 빠져나갑니다. 클릭을 가로채지 않습니다.

`MusicToast_view`의 `Shown Position`, `Enter Seconds`, `Display Seconds`, `Exit Seconds`를 인스펙터에서 조정할 수 있습니다. 기본값은 위치 `(32, 32)`, 등장 0.25초, 유지 3.5초, 퇴장 0.22초입니다. 긴 제목은 글자 크기를 줄이고 영역을 넘으면 말줄임합니다. 첫 곡과 반복 재생으로 다시 시작한 곡도 알림 대상입니다.

`BackgroundMusicPlayer`는 목록·로드·재생과 곡 변경 이벤트를 담당합니다. `MusicToastPresenter_presenter`는 이벤트를 받아 View를 갱신하고, `MusicToast_view`는 제목과 등장·퇴장만 담당합니다. 재생 목록이나 UI는 전투 오브젝트의 활성화와 분리되어 있습니다.

게임 일시정지와 재개 준비 중에도 BGM과 곡명 알림은 계속 진행합니다. BGM AudioSource만 `ignoreListenerPause = true`로 설정하므로 효과음은 기존 `AudioListener.pause`에 따라 정지하고, BGM 음량 설정은 일시정지 중에도 적용됩니다. 앱 백그라운드 전환 또는 포커스 이탈 시에는 `AudioSource.Pause()`로 재생 위치를 보존하고 곡명 알림도 멈춥니다. 앱이 다시 활성화되고 포커스도 돌아오면 `UnPause()`로 음악을 이어갑니다. 게임 자체는 기존처럼 수동 재개를 기다리며 음악만 먼저 이어집니다. 두 앱 상태를 함께 확인해 Pause·Focus 콜백 순서가 달라도 백그라운드에서 음악을 재개하지 않습니다.

최초 BGM 추가 작업은 코드·씬 파일 편집만 수행했으며, 당시 사용자 요청에 따라 컴파일·실행 검사와 새 테스트 작성은 진행하지 않았습니다. 2026-11-20 일시정지 정책 변경은 현재 프로젝트 참조와 Unity C# 컴파일러로 런타임 코드 컴파일을 확인했습니다. 실제 음악 청취·곡 전환과 Android 앱 전환·복귀 검사는 수행하지 않았습니다.
