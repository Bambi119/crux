# Hangar UI 프리팹 수동 제작 가이드 (H2 · H3)

> 작성: 2026-04-16
> 목적: 사용자가 Unity 에디터에서 Hangar UI 프리팹 6종을 수동 제작
> 예상 소요: 60~90분 (집중 시)
> 관련: `docs/10_ui_ux.md` §2 Hangar 와이어프레임
> 완료 후 이 파일은 이력 보존용. 삭제하지 말 것

## 왜 수동인가

에이전트(픽셀)에 Unity MCP로 위임하면 `create_game_object`·`add_component`·`set_rect_transform` 호출이 프리팹당 20~30회 누적 → 토큰 폭발 + MCP 타임아웃 위험. 사용자 수동 제작이 **빠르고 확실**. 에이전트는 이후 **C# 스크립트 작성**에 집중 (H4).

## 사전 준비

- Unity 에디터 실행, `Hangar.unity` 씬 열기
- Project 창에서 `Assets/_Project/Prefabs/` 확인 (EnemyTank·PlayerTank.prefab 있음)
- 새 폴더 생성: Project 창에서 `Prefabs` 우클릭 → `Create → Folder` → 이름 `UI`
- 최종 경로: `Assets/_Project/Prefabs/UI/`

## 공통 원칙

- **프리팹 제작은 Hierarchy에서 만든 후 Project 창으로 드래그** (가장 안전)
- **텍스트 컴포넌트는 `Text (Legacy)` 말고 `TextMeshPro - Text (UI)` 권장** (TMP). 처음 TMP 사용 시 "Import TMP Essentials" 버튼 한 번 눌러야 함
- **색상은 임시값**. 아트 패스 때 통일 예정
- **앵커는 컨텍스트별로 다름**. 각 프리팹 설명대로 설정
- 완성 후 Hierarchy에서 **Ctrl+S로 씬 저장**, 프리팹은 드래그 시 자동 저장

---

## H2-A. HangarPanel.prefab — 메인 3분할 레이아웃

### 루트 생성

1. MainCanvas 선택 → 우클릭 → `Create Empty` → 이름 `HangarPanel`
2. RectTransform 설정:
   - **Anchor Preset**: `stretch stretch` (Shift+Alt 누른 채 우하단 stretch 선택) → Top/Right/Bottom/Left 모두 0
3. Add Component → `Vertical Layout Group`
   - Padding: Top=0, Bottom=0, Left=0, Right=0
   - Spacing: 0
   - Control Child Size: Width ✓, Height ✗ (개별 제어)
   - Child Force Expand: Width ✓, Height ✗
4. Add Component → `Image` (배경용, 색 `#1E1E1E`)

### 자식 1: TopBar (상단 바)

1. HangarPanel 우클릭 → Create Empty → `TopBar`
2. RectTransform:
   - Width는 부모가 Stretch로 제어. Height = 60
3. Add Component → `Layout Element`
   - Min Height = 60, Preferred Height = 60
4. Add Component → `Horizontal Layout Group`
   - Padding: Left=20, Right=20, Top=10, Bottom=10
   - Spacing: 20
   - Control Child Size: ✓ ✓, Child Force Expand: ✗ ✗
5. Add Component → `Image` (배경 `#2A2A2A`)

#### TopBar 내부

- `MoneyText` (TMP Text): 내용 `자금: ₩0`, FontSize 20, 색 흰색
  - Layout Element Preferred Width = 200
- `MoraleText` (TMP Text): 내용 `사기: 0`, FontSize 20, 색 흰색
  - Preferred Width = 150
- `Spacer` (Empty GameObject + Layout Element flexible Width = 1) — 빈 공간 밀기
- `BackToMapButton` (UI → Button - TextMeshPro): 자식 Text 내용 `월드맵으로 돌아가기`
  - Preferred Width = 200, Height = 40

### 자식 2: MiddleRow (좌·중앙·우 3분할)

1. HangarPanel 우클릭 → Create Empty → `MiddleRow`
2. Layout Element: Flexible Height = 1 (나머지 세로 공간 다 차지)
3. Add Component → `Horizontal Layout Group`
   - Padding: 0, Spacing: 0
   - Control Child Size: ✓ ✓
   - Child Force Expand: Width ✗, Height ✓

#### 자식 2-a: LeftMenu (좌측 160px)

- Create Empty → `LeftMenu`
- Layout Element: Min Width = 160, Preferred Width = 160
- Image 배경 `#252525`
- Add Component → `Vertical Layout Group`
  - Padding: 10, Spacing: 5
  - Control Child Size ✓✓, Force Expand Width ✓
- **내용 비움** (탭 버튼은 H4 스크립트가 동적 주입)

#### 자식 2-b: CenterContent (중앙 flex)

- Create Empty → `CenterContent`
- Layout Element: Flexible Width = 1
- Image 배경 `#1E1E1E`
- **내용 비움** (탭별 콘텐츠 프리팹이 여기에 스왑됨)

#### 자식 2-c: RightPanel (우측 280px)

- Create Empty → `RightPanel`
- Layout Element: Min Width = 280, Preferred Width = 280
- Image 배경 `#252525`
- Add Component → `Vertical Layout Group`: Padding 10, Spacing 8
- **내용 비움** (H4 `HangarRightPanel.cs`가 전차 정보 채움)

### 프리팹 저장

- Hierarchy에서 `HangarPanel` 드래그 → Project `Assets/_Project/Prefabs/UI/` 폴더에 드롭
- Hierarchy의 HangarPanel은 씬 인스턴스로 남김 (H5에서 사용)

---

## H2-B. CompositionTab.prefab — 편성 탭 콘텐츠

### 루트

1. Hierarchy 임의 위치에 Create Empty → `CompositionTab`
2. RectTransform Anchor: stretch stretch
3. Add Component → `Vertical Layout Group`
   - Padding 20, Spacing 10
   - Control Child Size ✓ ✓, Force Expand Width ✓

### 자식 계층

```
CompositionTab
├─ LaunchSlotsLabel (TMP Text "출격 슬롯 (3/5)", FontSize 18)
├─ LaunchSlotsRow (Empty + HorizontalLayoutGroup, Padding 0, Spacing 10, Control Size ✓✓)
│     └─ (비움 — H4가 TankSlot 5개 동적 주입)
├─ StorageSlotsLabel (TMP Text "보관 (5/5)")
├─ StorageSlotsRow (Empty + HorizontalLayoutGroup, 위와 동일)
│     └─ (비움)
└─ ActionRow (Empty + HorizontalLayoutGroup, Padding Top=20, Spacing 15)
      ├─ OpenInventoryButton (TMP Button, 텍스트 "파츠 인벤토리 열기", Preferred Width 220, Height 40)
      └─ CompatibilityCheckButton (TMP Button, 텍스트 "호환성 검사", Preferred Width 180, Height 40)
```

### Row LayoutElement

- LaunchSlotsRow / StorageSlotsRow 둘 다 Layout Element **Preferred Height = 140** (TankSlot 높이와 동일)

### 프리팹 저장

Hierarchy의 CompositionTab → Project 드래그 → `UI/CompositionTab.prefab`. Hierarchy에서 삭제 가능 (프리팹만 남기면 됨).

---

## H2-C. TankSlot.prefab — 전차 슬롯 재사용 단위

### 루트

1. Create Empty → `TankSlot`
2. RectTransform: Width=120, Height=140, Anchor=center center
3. Layout Element: Preferred Width=120, Preferred Height=140
4. Add Component → `Image` (배경 `#3A3A3A`)
5. Add Component → `Button`
   - Target Graphic = 방금 추가한 Image
   - Transition: Color Tint
   - Normal `#3A3A3A`, Highlighted `#4A4A4A`, Pressed `#2A2A2A`, Selected `#5A5A5A`

### 자식

- `TankNameText` (TMP Text)
  - 내용 `없음` (기본, 런타임에 교체)
  - FontSize 14, 색 흰색
  - Alignment Center Center
  - RectTransform: Anchor=bottom stretch, Left 5, Right 5, Bottom 5, Height 30

### 프리팹 저장

Hierarchy의 TankSlot → Project 드래그 → `UI/TankSlot.prefab`. Hierarchy 인스턴스 삭제.

---

## H2-D. LockedTabPanel.prefab — 잠금 탭 공용 오버레이

### 루트

1. Create Empty → `LockedTabPanel`
2. RectTransform: Anchor stretch stretch, 사방 0
3. Add Component → `Image`
   - Color `#000000` Alpha = 180 (0~255 기준, 반투명)

### 자식

- `LockIcon` (TMP Text)
  - 내용 `🔒` (이모지로 대체. 아이콘 에셋 나중에)
  - FontSize 48
  - Alignment Center
  - RectTransform: Anchor center center, Width 80, Height 80, Pos Y = 40
- `ComingSoonText` (TMP Text)
  - 내용 `곧 출시 예정`
  - FontSize 22, 색 `#CCCCCC`
  - Alignment Center
  - RectTransform: Anchor center center, Width 300, Height 40, Pos Y = -40

### 프리팹 저장

`UI/LockedTabPanel.prefab`. 이 프리팹 1개로 정비·상점·스킬·회식·인물 탭 5개 공용.

---

## H3-A. PartInventoryPanel.prefab — 파츠 인벤토리 오버레이

> docs/10 §2.3 참조. OverlayCanvas 하위에 배치될 오버레이.

### 루트

1. Create Empty → `PartInventoryPanel`
2. RectTransform: Anchor stretch stretch, 사방 60 (씬 전체에서 60px 여백 두고 덮는 오버레이)
3. Add Component → `Image` (배경 `#1A1A1A` Alpha 230)
4. Add Component → `Vertical Layout Group`
   - Padding 20, Spacing 10, Control Size ✓✓, Force Expand Width ✓

### 자식 계층

```
PartInventoryPanel
├─ HeaderRow (Empty + HorizontalLayoutGroup, Spacing 10)
│   ├─ TargetTankDropdown (UI → Dropdown - TextMeshPro, Preferred Width 200)
│   │     └─ 옵션은 H4 스크립트에서 동적 채움. 기본 표시 "로시난테 ▼"
│   ├─ Spacer (flex=1)
│   └─ CloseButton (TMP Button "X", Preferred Width 40, Height 40)
├─ BodyRow (Empty + HorizontalLayoutGroup, LayoutElement flexibleHeight=1)
│   ├─ TankModelImage (Image, 240x240, 배경 #2A2A2A, 실 아트 없으니 플레이스홀더)
│   │     Layout Element Preferred Width 240
│   ├─ SlotColumn (Empty + VerticalLayoutGroup, Spacing 5, flexibleWidth=1)
│   │     └─ (비움 — H4가 PartSlotItem 5개 주입: 주포·장갑·엔진·궤도·보조)
│   └─ PartListScrollView (UI → Scroll View)
│         Layout Element Preferred Width 300
│         Viewport / Content 구조는 ScrollView 기본 유지
│         Content에 VerticalLayoutGroup + ContentSizeFitter(Vertical=PreferredSize)
│         → H4가 여기에 파츠 후보 동적 생성
└─ FooterRow (Empty + HorizontalLayoutGroup, Padding Top 10, Spacing 10)
    └─ FilterToggle (UI → Toggle, 텍스트 "호환만 보기", 기본 ON)
```

### Scroll View 팁

- `UI → Scroll View` 생성 시 ScrollView / Viewport / Content / Scrollbar Horizontal·Vertical 자동 생성
- Horizontal 스크롤 비활성화 → ScrollView 오브젝트에서 `Horizontal` 체크 해제, Scrollbar Horizontal 삭제
- Content의 VerticalLayoutGroup: Padding 5, Spacing 3, Control Size ✓✓, Force Expand Width ✓

### 프리팹 저장

`UI/PartInventoryPanel.prefab`. 이 프리팹은 **OverlayCanvas 하위에 인스턴스화**되며 기본 비활성(SetActive false). [파츠 인벤토리 열기] 버튼에서 활성화.

---

## H3-B. PartSlotItem.prefab — 슬롯 행 재사용 단위

### 루트

1. Create Empty → `PartSlotItem`
2. RectTransform: 기본 (Layout Group가 제어)
3. Layout Element: Preferred Height = 40
4. Add Component → `Horizontal Layout Group`: Padding 5, Spacing 10, Control Size ✓✓, Force Expand Height ✓
5. Add Component → `Image` (배경 `#2A2A2A`)

### 자식

```
PartSlotItem
├─ SlotCategoryText (TMP Text, 내용 "주포:", LayoutElement Preferred Width 80)
├─ CurrentPartText (TMP Text, 내용 "(비어 있음)", flexibleWidth=1)
├─ CompatibilityIcon (TMP Text, 내용 "✓" 또는 "✗", Preferred Width 30)
└─ SwapButton (TMP Button, 텍스트 "교체", Preferred Width 70, Height 30)
```

### 프리팹 저장

`UI/PartSlotItem.prefab`. H4 `CompositionTabController`가 카테고리별로 5개 인스턴스화.

---

## 완료 체크리스트 (사용자 자체 확인)

프리팹 6개 모두 생성 후:

- [ ] `Assets/_Project/Prefabs/UI/` 폴더에 `.prefab` 6개
  - HangarPanel · CompositionTab · TankSlot · LockedTabPanel · PartInventoryPanel · PartSlotItem
- [ ] 각 프리팹 더블클릭 → Prefab Mode 진입 시 에러 없이 열림
- [ ] HangarPanel 계층: TopBar·MiddleRow(LeftMenu·CenterContent·RightPanel) 구조 시각 확인
- [ ] CompositionTab 에 LaunchSlotsRow / StorageSlotsRow / ActionRow 3개
- [ ] LayoutGroup 경고 (Graphic 없는 자식 등) **없음**
- [ ] **씬 저장** (Ctrl+S)

## 완료 후 다음 단계

1. 사용자 → 나에게 "프리팹 완료" 알림
2. 나 → 프리팹 상태 Grep 검증 (에이전트 위임 없이 직접) + `Hangar.unity` 현재 상태 Grep
3. H4: 픽셀에게 **C# 스크립트 3종 작성** 위임 (MCP 불필요, Write/Edit만 → 토큰 효율)
4. H5: 사용자 → Inspector에서 프리팹 ↔ 스크립트 필드 할당 + Play 테스트
5. 검증 통과 → 단일 커밋

## Tip

- 처음 제작 시 1~2개 프리팹에서 LayoutGroup 동작 원리 익혀두면 나머지 속도 빨라짐
- "Preferred Width/Height"가 먹지 않으면 부모 LayoutGroup의 "Control Child Size" 체크 확인
- 앵커가 꼬이면 Alt/Shift 누른 채 Anchor Preset 재선택
- TMP 컴포넌트 필드가 안 보이면 "Convert to TMP Text" 또는 처음 TMP 임포트 확인
