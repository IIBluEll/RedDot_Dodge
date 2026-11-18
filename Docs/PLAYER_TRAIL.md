# 플레이어 네온 꼬리

`PlayerMovement`가 실행 중 `PlayerTrail`을 자동으로 붙인다. MainPlay의 별도 Inspector 연결은 필요하지 않다. 원본 `player_trail_right.png`의 청록·파랑·보라 색감과 끝이 가늘어지는 형태를 참고하고, 꺾인 이동 경로를 따라 휘어지도록 TrailRenderer로 표현한다.

- 물리 이동 결과를 직접 기록하므로 마우스·기울기·벽 반동에 함께 적용된다.
- 플레이어 뒤 0.16 유닛에서 시작한다. 속도에 따라 두께 0.12~0.36, 유지 시간 0.12~0.22초, 네온 강도가 달라진다.
- 멈추거나 사망하면 새 점을 추가하지 않고 기존 흔적이 수명에 따라 사라진다. 이동 판정에는 영향을 주지 않는다.
- 재시작·이동 재개 시 흔적을 지운다. 비정상적으로 큰 위치 이동도 꼬리로 연결하지 않는다.
- 밝은 중심선과 부드러운 가장자리를 전용 URP 셰이더로 그린다. MainPlay의 기존 Bloom이 밝은 부분의 빛 번짐을 더한다.
- 재질은 `Assets/Main/Resources/PlayerTrailNeon.mat`에서 로드해 빌드에 함께 포함한다. 원본 PNG와 기존 카메라·프로젝트 설정은 수정하지 않는다.

## 조정

기본값은 `PlayerTrail.cs`의 직렬화 필드에 있다. 편집 모드에서 Player에 PlayerTrail을 미리 추가하면 Inspector에서 재질·유지 시간·두께·뒤쪽 거리·최소 속도를 조절해 저장할 수 있다. 실행 중 자동 추가된 컴포넌트의 Inspector 수정은 Play 종료 시 사라진다.

## 검증 범위

Unity 6000.3.20f1의 C# 컴파일러와 프로젝트 참조로 런타임 코드 컴파일을 통과했다. 재질의 셰이더 GUID 연결, 설치된 URP 2D의 셰이더 패스 지원, 이동·재시작 호출 경로를 검토했다.

초기 전달 이후 Unity 로그에서 `MaterialPropertyBlock`을 필드 초기화 구문에서 생성할 수 없다는 예외와 뒤이은 `RecordMovement`의 NullReferenceException을 확인했다. 생성 위치를 `Awake`로 옮기고, Play 중 스크립트 재로드로 참조가 사라졌을 때 이동 함수에서 재생성하도록 수정했다.

격리 프로젝트의 실제 Play Mode에서 컴포넌트 자동 추가·이동 기록, 프로퍼티 블록 재생성, 기존 컴포넌트 중복 방지, 꼬리 초기화 후 이동을 확인했다. 결과는 `Temp/CodexPlayerTrail/InitializationCheck/result.json`에 있다. 이 검사는 초기화 경로에 집중하기 위해 기본 재질을 사용했다.

이후 MainPlay의 실제 네온 재질로 화면을 비교해, 수동으로 점을 추가하면서 `emitting = false`로 둔 상태에서는 메시가 있어도 화면에 나타나지 않는 문제를 재현했다. 방출을 활성화하고 TrailRenderer의 위치를 플레이어 뒤쪽의 실제 기록점과 일치시켰다. 회전 후 기록하도록 호출 순서를 맞춰 방향 전환 시 방출 위치가 어긋나지 않도록 했다.

`RedDot Dodge > Check Player Trail Rendering`은 현재 씬에서 짧은 이동을 구성한 뒤, 꼬리 표시 전후의 화면 픽셀을 비교한다. 결과와 PNG는 `ArtExports/PlayerTrailQA`에 저장하고, 확인할 수 있도록 Play를 일시정지한다. 확인 후 Play를 종료하면 원래 씬으로 돌아간다. Android 실기기 검사는 수행하지 않았다.

최종 수정 후 실제 MainPlay의 곡선 이동에서 꼬리가 보이는 것을 확인했다. 꼬리를 켠 화면과 끈 화면의 비교에서 2,314개 픽셀이 달랐으며, 결과는 `ArtExports/PlayerTrailQA/PlayerTrail-check.txt`, 이미지는 `PlayerTrail.png`에 저장했다. 검사를 마친 뒤 Play를 종료했다.
