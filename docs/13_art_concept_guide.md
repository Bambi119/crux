# 13. 아트 콘셉트 가이드

> 작성: 2026-04-29 — 초판 v0.1 (스타일 명세 우선 트랙)
> 관련 문서: `02_worldbuilding.md` (톤·세계관) · `06_combat_system.md §8` (지형 종류) · `09_map_stage.md §1.4` (지역 분포) · `10_ui_ux.md` (UI 시각 톤)
> **상위 결정**: AI 이미지 생성(ChatGPT 5.5 또는 Coplay MCP `generate_or_edit_images`)으로 1차 산출 → Aseprite·Photoshop 후처리 → Unity 임포트

## 0. 이 문서의 범위

이 문서는 **AI 이미지 생성으로 맵 타일과 전투 씬 보조 비주얼을 만들기 위한 스타일 명세**를 정의한다.

- ✅ 포함: 톤·컬러 팔레트·시점·라이팅·픽셀 해상도·hex 타일 기하·후처리 기준
- ✅ 포함: AI 생성 프롬프트의 **공통 헤더**가 될 스타일 키워드 셋
- ⏸ 후속: 지형별 프롬프트 템플릿 카탈로그(§7) · 캐릭터 초상화 스타일 · 적 차량 컨셉 시트
- 🚫 비포함: VFX·이펙트 스타일(별도 가이드) · UI 위젯 시각(`docs/10` 위임)

---

## 1. 톤 & 무드 — 세계관 v2.0 압축

`docs/02 v2.0` 세계관 핵심 정서를 시각 언어로 환원한다.

| 축 | 키워드 | 시각 표현 |
|---|---|---|
| **세계 상태** | 자업자득의 폐허 / 봉쇄 / 격리구 / 사하라 동부 | 마른 흙, 균열 콘크리트, 모래 바람의 흔적, 녹슨 철제 잔해 |
| **시간 감각** | 전쟁 직후 ~ 진행 중 / 한 세대 이내 | 폐허는 풍화되었지만 부서진 지 오래 안 됨. 잿빛은 짙되 화석화는 아님 |
| **돈키호테 모티프** | 낡고 우스꽝스럽고 손수 만든 것 | 로시난테는 깡통의 위엄, 코드키는 손에 닿는 평범한 데이터 카트리지 |
| **희망의 결** | 비참하지만 무겁지만은 않다 | 따뜻한 황혼·창가의 빛·아이들의 옷자락 같은 작은 채도 포인트 |
| **엔딩 회수** | 의료로의 귀환 / 평범한 일상 | 후반부 일부 타일·배경에 **녹·흙의 색에서 풀·물의 색으로** 전환 여지 |

**한 줄 요약**: *"녹슬고 메마른 격리구 풍경에 작은 따뜻한 채도가 점처럼 박혀 있는 톱다운 픽셀 아트."*

---

## 2. 시각 스타일 결정

### 2.1 결정안 — 픽셀 아트 (Pixel Art)

| 결정 | 값 | 근거 |
|---|---|---|
| 스타일 | **픽셀 아트 (제한 팔레트)** | 1인 인디 스코프 / 헥스 타일 명료성 / 기존 `TerrainTestScene` 톤 연속 / AI 생성 후 `pixelization` 후처리가 가장 안정 |
| 베이스 해상도 | **256×222 px** (헥스 타일 기준, §5 참조) | flat-top hex size=128 시 너비/높이 |
| 픽셀 그리드 | **2x 픽셀** (논리 픽셀 1 = 화면 픽셀 2) | 작은 디테일과 가독성 균형. 세로 222 → 논리 111 라인 정도 |
| 디더링 정책 | **SNES 16비트 톤의 디더링 허용** (면 그라데이션·물 표면·먼지·금속 광택) | Front Mission 1 레퍼런스 채택 — 면 채움 디더링이 톤 일관성에 기여. 단, 헥스 가장자리 1 px는 디더링 금지 (마스크 깔끔 유지) |
| 외곽선 | **변마다 0~1 px 다크 라인** (없을 수도 있음) | 헥스 타일 가장자리는 마스크가 처리하므로 외곽선 의무 아님 |
| 안티에일리어싱 | **금지** | Unity Filter Mode = Point 전제. AA가 들어가면 픽셀 아트가 블러 처리됨 |

### 2.2 대안 (보류 — 사용자 검토 필요)

- **B안: 페인터리 (Painterly)** — Banner Saga 풍 수채/아크릴 톱다운. 표현 범위 넓지만 1인 분량으로 일관성 유지가 매우 어려움
- **C안: 픽셀+페인터리 하이브리드** — 타일은 픽셀, 배경은 페인터리. 가능하지만 톤 불일치 위험

> 본 문서는 **A안 픽셀 아트**를 채택해 진행한다. 사용자 검토 후 변경 가능.

---

## 3. 컬러 팔레트

### 3.1 확정 — 안 D · Front Mission 1 (Huffman Tactical)

> 2026-04-29 사용자 지시로 Front Mission 1 (SNES, 1995, Squaresoft) 전술 맵 톤을 레퍼런스로 채택.
> 무광 군용 올리브 + 마른 카키 + 콘크리트 회색 + 차가운 강철 청회를 베이스로, 녹슨 적색·따뜻한 베이지를 액센트로 사용.
> 게임 전체에서 이 팔레트 외 색은 **VFX·UI 강조에만** 허용.

#### 8색 베이스

| 역할 | Hex | 사용처 | FM1 매핑 |
|---|---|---|---|
| **깊은 그늘** | `#1A1814` | 모든 그림자 최암부, 외곽 라인 | 차체 음영, 건물 내부 어두운 모서리 |
| **어두운 올리브** | `#3D4825` | 숲 그림자, 군용 도장 음영 | 숲·관목 타일 어두운 면, 정부군 차체 그림자 |
| **올리브 그린** | `#6E7335` | 풀·관목·군용 도장 베이스 | FM1 풀 타일·완저 군용 도장 |
| **카키 탄** | `#A88E5A` | **베이스 지면 톤** (마른 흙·도로 가장자리·모래 어두움) | FM1 평지 베이스 |
| **콘크리트 그레이** | `#8A8A86` | 콘크리트·아스팔트·건물 외벽·기계 본체 | FM1 도로·빌딩 |
| **강철 청회** | `#3E5A78` | 물·금속 그림자·찬 그늘 | FM1 물 타일·기계 음영 |
| **녹슨 적색** | `#A0502E` | 철제 부식·녹·낡은 페인트·소량 강조 | FM1 표지·녹슨 컨테이너 |
| **하이라이트 베이지** | `#D4BC8E` | 햇빛 받은 모래·가장자리 디더링 하이라이트 | FM1 사막 타일 밝은 면 |

#### 액센트 — 팔레트 외 허용 (제한 사용)

| 용도 | 색 | 비고 |
|---|---|---|
| 적 차체 식별 (열혈 강조) | `#C04030` 류 | 위 녹슨 적색과 차별. 차량 라이트·식별 마커에만 |
| 아군 차체 식별 | `#3070A0` 류 | 위 강철 청회보다 채도 높음. 차량 라이트·식별 마커에만 |
| 폭발 VFX | 백·황 그라데이션 | 팔레트 무관 (이펙트 레이어) |

> 적/아군 라이트 색은 docs/04 트레잇·마크 시각 표시와 정합 검토 필요.

### 3.2 적용 원칙 (FM1 레퍼런스에서 도출)

- **무광 (Matte)** — 광택 표현 최소화. 금속도 광택보다 색 전환으로 표현
- **저채도 (Muted)** — 8색 모두 채도 50% 이하. 강조도 채도 70% 미만
- **면 단위 색** — 한 타일 안에서 베이스 1색 + 그림자 1색 + 하이라이트 1색이 표준 (3톤 셰이딩)
- **디더링 적극 활용** — SNES 시대 그라데이션 대용. 두 베이스 색 사이 1~2픽셀 산포로 자연스러운 톤 전환
- **하이라이트는 점·라인** — 면 채움 하이라이트 금지. 하이라이트 베이지는 가장자리 1~2 px 또는 디더링 산포

### 3.3 검토했던 이전 후보안 (참고)

이전 v0.1 초안에서 검토되었으나 FM1 레퍼런스 채택으로 폐기. 향후 톤 시프트 필요 시 참고용.

- **안 A — 격리구 황혼 (Quarantine Dusk)**: 따뜻한 황혼·황색 강조. FM1 대비 채도와 따뜻함이 과도
- **안 B — 사막 봉쇄 (Sand Embargo)**: 황색 비중 ↑ 회색 ↓. FM1 대비 폐허감 약함
- **안 C — 잿빛 격리 (Ash Quarantine)**: 회청 비중 극대. FM1 대비 군용 올리브 부재로 전쟁 톤 약함

### 3.4 후반부 회복 톤 시프트

`docs/02 §11 엔딩` 회수 구조에 맞춰 후반부 일부 타일·배경에 톤 시프트 여지를 둔다.

- 올리브 그린의 **채도 +20%** 변형 1색 추가 가능 (`#7E8A38` 정도) → 살아 있는 풀
- 강철 청회의 **명도 +15%** 변형 1색 추가 가능 (`#5278A0` 정도) → 맑은 물
- 카키 탄은 그대로 유지 (땅은 변하지 않는다)

> 정확한 시프트 폭은 후반부 컨텐츠 작업 시 §3.1 베이스를 손대지 않고 별도 변형으로 추가.

### 3.5 GPT 생성 프롬프트 초안 (참고용 시안 산출)

> 본 팔레트가 시각적으로 어떻게 작동하는지 GPT-5.5 등 외부 이미지 모델로 **참고 시안**을 빠르게 뽑기 위한 복붙용 프롬프트.
> 산출물은 최종 에셋이 아니라 **톤·면 분할·디더링 참조용**. 실제 타일은 §9 후처리 파이프라인을 거친다.

#### 3.5.1 팔레트 시안 1장 (8색 색상 칩 + 사용 예시)

```
Create a single reference sheet showing a custom 8-color pixel art palette
inspired by Front Mission 1 (SNES, 1995, Squaresoft) tactical map tone.

Layout: 8 horizontal color swatches at the top, each labeled with its hex code
and role. Below the swatches, show 4 small example pixel art hex tiles
(flat-top hexagons, ~256x222 px each) demonstrating the palette in action:
1. Dry cracked earth ground tile
2. Cracked asphalt road tile
3. Shallow muddy water puddle tile
4. Concrete rubble pile tile

Palette (must use ONLY these 8 colors plus transparency):
- #1A1814 deep shadow (darkest outline, deepest crevices)
- #3D4825 dark olive (forest shadow, military paint shadow)
- #6E7335 olive green (grass, foliage, military base coat)
- #A88E5A khaki tan (base earth tone, dusty ground)
- #8A8A86 concrete gray (asphalt, building walls, machinery)
- #3E5A78 steel blue-gray (water, cold metal shadow)
- #A0502E rust red (corroded steel, faded paint accent)
- #D4BC8E highlight beige (sunlit sand, dithered highlight)

Style rules:
- Top-down orthographic viewpoint
- Matte, low-saturation, military aesthetic
- 16-bit SNES-era dithering allowed for face gradients
- Single light source from upper-left, short shadow toward lower-right
- No anti-aliasing, sharp 1-pixel edges
- 3-tone shading per element: base color + shadow + highlight
- Transparent background outside hex shapes
- NO text inside the tiles, NO UI elements, NO characters, NO vehicles
- Labels and hex codes in plain sans-serif outside the tiles

Output: 1024x1024 px reference sheet, sharp pixel edges, no smoothing.
```

#### 3.5.2 단일 타일 1장 (지형별 변형 시도용)

```
Create a single top-down pixel art tile of [TERRAIN_NAME] for a turn-based
tactical game inspired by Front Mission 1 (SNES) palette.

Tile shape: flat-top hexagon, 256x222 px, transparent background outside hex.
Limited to this 8-color palette ONLY:
#1A1814, #3D4825, #6E7335, #A88E5A, #8A8A86, #3E5A78, #A0502E, #D4BC8E.

Terrain content: [TERRAIN_DESCRIPTION — see §8.1 for per-terrain text]

Style:
- Matte, low-saturation, military post-apocalyptic
- 16-bit SNES dithering allowed for face gradients (water, dust, metal)
- Light source upper-left, short shadow lower-right
- 3-tone shading: base + shadow + highlight per element
- No anti-aliasing, sharp pixel edges
- Seamless edges that tile naturally with neighbors
- NO text, NO UI elements, NO characters, NO vehicles

Output: 1024x888 px (will be downsampled to 256x222 in post).
```

> `[TERRAIN_NAME]` / `[TERRAIN_DESCRIPTION]` 슬롯은 §8.1 표의 "추가 프롬프트" 한 줄로 채운다.
> 예: `TERRAIN_NAME = cracked asphalt road`, `TERRAIN_DESCRIPTION = cracked asphalt road surface, faded white lane marks, scattered debris`.

#### 3.5.3 사용 절차

1. §3.5.1 프롬프트로 팔레트 시안 1장 → 8색 적용 결과 시각 확인 → 팔레트 미세 조정 결정
2. §3.5.2 프롬프트로 마른 흙 1장만 먼저 → 톤 OK면 나머지 5종 일괄
3. 결과 PNG는 `Crux-planning/tmp/art-drafts/` (gitignore 대상)에만 두고 본 워크트리에 커밋 금지
4. 최종 에셋은 dev 워크트리 `Assets/_Project/Sprites/Tiles/` 에 §9 파이프라인 거쳐서만 진입

---

## 4. 시점 & 라이팅

### 4.1 시점 (Viewpoint)

- **톱다운 직교 투영** (Top-Down Orthographic) — 기울임 0°
- 헥스 타일 평면을 정면에서 내려다보는 시점
- **iso 변형 금지** — 현재 워크트리는 직교 그리드 전제 (D-01 iso hex 검토는 별도 트랙)
- 차량·사물의 측면 디테일은 **상단 및 측면 일부 노출** 정도까지만 표현 (완전 평면 X, 완전 입체 X)

### 4.2 라이팅 방향

- **광원 방향 통일**: 좌상단 → 우하단 (북서 → 남동)
- 타일·오브젝트의 그림자는 **우하단으로 짧게**
- 그림자 색: 베이스 색의 어두운 변형 (별도 회색 사용 X — 팔레트 외 색 회피)
- **헥스 타일 자체에 그림자 굽지 말 것** — 그림자는 오브젝트 레이어가 담당

### 4.3 시간대

- **고정 황혼 (Eternal Dusk)** — 한 캠페인 내내 시간대 변경 없음 (1인 스코프 보호)
- 회복 단계 후반부도 시간대 동일, 단 **풀·물 비중**을 늘려 분위기 변화 표현

---

## 5. Hex 타일 기하 (Hex Tile Geometry)

### 5.1 결정 사양

| 항목 | 값 |
|---|---|
| 타일 형태 | flat-top hex (위·아래가 평평한 변) |
| 좌표계 | odd-q offset (`docs/06 §1`과 일치) |
| 타일 크기 | **256×222 px** (size=128) |
| 색 배경 | **투명** (PNG with alpha) |
| 시임리스 | 인접 6변에서 색·텍스처가 자연스럽게 잇닿아야 함 |
| 내부 마진 | 타일 가장자리 **2 px 안전 마진** 권장 (안티앨리어스 잔여물 방지) |
| 그리드 정렬 | flat-top 규칙: 가로 stride = 192 px (size×1.5), 세로 stride = 222 px |

### 5.2 타일 마스크

- 모든 타일은 동일한 헥스 마스크로 자른다 — 마스크 PNG 1장을 후처리에서 곱연산
- 마스크 외부는 알파 0
- 마스크 안쪽 1 px은 안티앨리어스 가능한 **소프트 엣지** (Unity Point 필터에서도 깔끔히 잘림)

### 5.3 AI 생성 시 입력 해상도

- **소스 생성: 1024×888 또는 2048×1776** (8x 또는 4x 다운샘플 후 256×222로 도달)
- 이유: AI 생성기는 작은 해상도에서 디테일이 무너짐. 큰 캔버스 → 다운샘플이 픽셀 아트 변환 품질 더 좋음
- 다운샘플 후 **인덱스드 컬러 팔레트로 양자화** (§3 확정 팔레트, 8~16색)

---

## 6. 지형 타일 카탈로그

### 6.1 1차 타깃 (`docs/06 §8.1` 지형 효과 6종 + 변형)

| # | 지형 | 데이터상 효과 (06 §8.1) | 시각 변형 수 (1차) |
|---|---|---|---|
| 1 | **아스팔트** (Asphalt) | 기본 — 패널티 없음 | 3 (균열 약/중/심함) |
| 2 | **진흙** (Mud) | 이동 AP +1 | 2 (얕은/깊은) |
| 3 | **잔해** (Rubble) | 이동 AP +1, 시야 차단 | 3 (콘크리트/금속/혼합) |
| 4 | **물웅덩이** (Water Puddle) | 이동 AP +1, 엔진 과열 경감 | 2 (작은/큰) |
| 5 | **건물 내부** (Building Interior) | 시야 차단, 엄폐 유효 | 3 (잔존 벽/완파 벽/지붕) |
| 6 | **마른 흙** (Dry Soil) | 기본 (격리구 평지 베이스) | 4 (균열 0~3단계) |

> 합계 1차 17종. 동일 지형 내 변형은 회전·플립으로 4× 증식 가능 → 실제 변형 ~50+ 효과.

### 6.2 2차 타깃 (지역 변형, `docs/09 §1.4` 4~6 지역)

지역별로 위 6종이 색·텍스처 변형으로 갈라진다.

| 지역 | 베이스 톤 시프트 | 특수 타일 |
|---|---|---|
| 격리구 마을 (1막) | 기본 팔레트 | 부서진 가옥 외벽·우물 |
| 사막 폐허 (1~2막) | 모래·황색 비중 ↑ | 모래 언덕·뼈만 남은 차체 |
| 폐허 도시 (2막) | 회색·콘크리트 ↑ | 무너진 빌딩 코너·녹슨 도로 표지 |
| 숲 외곽 (2~3막) | 풀 녹색 도입 | 죽은 나무·관목·이끼 바위 |
| 위성 시설 인근 (3막) | 차가운 청회·백색 ↑ | 안테나 잔해·콘크리트 벙커 |

### 6.3 파괴 가능 오브젝트 (별도 레이어)

- 건물 잔해·바리케이드·차량 잔해 (`docs/06 §8.2`)
- **타일이 아닌 오브젝트 스프라이트** — 헥스 위에 별도 z-order로 얹음
- 본 1차 스프라이트 생성 트랙에 **포함하지 않음** (별도 트랙)

### 6.4 샘플 지역 1차 — 케마르 격리구 마을 (1막 무대)

> 출처: `docs/02:20` (Khemar Enclave 가자 모델) · `docs/02:147-149` (1막 시작 환경) · `docs/06 §5+§8` (지형·엄폐) · `docs/05:23-26` (차체 5종)
> **목표**: 단일 지역 1세트로 GPT 시안 → 톤 검증 → 합격 시 동일 지역의 본 에셋·배틀씬 외곽 보더로 승격.

#### 6.4.1 지역 컨셉 압축

- **지정학**: 티벨란 공화국 동부, 봉쇄된 케마르 격리구. 가자 지구형 봉쇄·검문소·고립
- **건축**: 폐허 군수 창고(콘크리트 외벽 일부 잔존), 폐허 의료 시설(타일 바닥 잔해), 마을 가옥(흙벽돌·녹슨 양철)
- **표면**: 마른 흙 도로 위주, 단편적 균열 아스팔트, 우물·드럼통 주변 진흙·물웅덩이
- **식생**: 거의 없음 (1막), 죽은 나무·관목 잔해만
- **색채 적용**: FM1 안 D §3.1 직접. 카키 탄·콘크리트 그레이 비중 ↑, 강철 청회는 물웅덩이/금속 음영에만

#### 6.4.2 산출 SKU 카탈로그 (39종)

| 카테고리 | SKU 접두 | 변형 | 합계 | 비고 |
|---|---|---|---|---|
| 지면 — 마른 흙 | `soil_dry_` | a · b · c · d (균열 0~3) | 4 | 베이스 지면 |
| 지면 — 균열 아스팔트 | `road_asphalt_` | a · b · c (약·중·심) | 3 | 마을 도로 |
| 지면 — 잔해 | `rubble_` | a · b · c (콘크리트·금속·혼합) | 3 | 무너진 건물 자리 |
| 지면 — 진흙 | `mud_` | shallow · deep | 2 | 우기 흔적 |
| 지면 — 물웅덩이 | `water_puddle_` | a (작은 우물) | 1 | 강철 청회 활용 |
| 지면 — 건물 내부 바닥 | `floor_interior_` | tile · concrete · plank | 3 | 폐허 의료 시설 |
| **엄폐물 — Small (1면)** | `cover_sandbag_` | n · ne · se · s · sw · nw | 6 | 모래주머니 (카키 탄+녹슨 적색) |
| **엄폐물 — Medium (3면)** | `cover_wall_` | n · ne · se · s · sw · nw | 6 | 콘크리트 외벽 절단 (콘크리트 그레이) |
| **엄폐물 — Medium (3면)** | `cover_container_` | n · ne · se · s · sw · nw | 6 | 녹슨 화물 컨테이너 (녹슨 적색) |
| 풍경 오브젝트 | `prop_` | dead_tree · well · fuel_drum · debris_pile | 4 | hex 위 z-order |
| 차체 스케일 검증 | `tank_assault_top` | rocinante (Assault 중형) | 1 | 배치 비례 검증용 |
|  |  | **합계** | **39** |  |

#### 6.4.3 스케일·픽셀 비례 (256×222 헥스 기준)

| 대상 | 권장 px | 근거 |
|---|---|---|
| 헥스 내경 | 256(가로 점-점) × 222(세로 변-변) | §5.1 |
| 차체 — Assault 중형 (베이스) | 길이 **160** × 폭 **96** | hex 회전 시 잘림 마진 ±30 px |
| 차체 — 5종 비례 | Scout ×0.80 / Assault ×1.00 / Support ×1.05 / Heavy ×1.15 / Siege ×1.30 | `docs/05:23-26` 하중 60→220 비율 압축 |
| 엄폐물 — Small (1면) | 변 길이 **128** × 두께 **24** | hex 한 변(=128) 따라 띠 |
| 엄폐물 — Medium (3면) | 변 길이 **128** × 두께 **40** × 면당 1장 (3면 일체형 또는 분할) | 3면 결합 시 ~코너 형태 |
| 풍경 오브젝트 | 64×64 ~ 96×96 | hex 중앙에 z-order 배치 |

> 픽셀 수치는 권장값. dev 측 `GameConstants`/실제 PPU와 정합 시 ±10% 보정 가능. §11 #7 결정 항목.

#### 6.4.4 엄폐물 방향 규약 (6변)

- flat-top hex 기준 6변 = **N(상)·NE(우상)·SE(우하)·S(하)·SW(좌하)·NW(좌상)**
- `docs/06:241-253` 변(edge) 속성 모델과 1:1 일치. 한 셀 최대 6면 플래그
- **생성 효율**: 베이스 1방향(예: N) 디자인을 60°·120°·180°·240°·300° 회전으로 5변 파생 → SKU당 6장
- HP 단계별 손상 변형(`docs/06:244-245` 3면→2면→1면)은 1차 트랙 **제외**. 만전 상태만 생성, 후속 트랙에서 손상 변형 추가
- Large(3면 초과 요새급)는 별도 구조물 카테고리(`docs/06:258-263`) — 1차 미포함

---

## 7. 전투 씬 보조 비주얼

> 톱다운 헥스 그리드는 화면 전체를 덮으므로 **전통적 의미의 "배경"은 작다**. 본 절은 사용자가 요청한 "베틀씬 배경"의 실제 산출 항목을 정의.

### 7.1 산출 후보 (사용자 결정 대기)

| 후보 | 설명 | 우선도 |
|---|---|---|
| **a. 맵 외곽 보더** | 헥스 그리드 가장자리 너머 영역. 멀리 흐려진 배경(산·하늘·잔해 라인) | ⭐⭐⭐ 높음 |
| b. 패럴럭스 스카이박스 | 카메라 패닝 시 미세 시차 이동. 톱다운에서는 효과 약함 | ⭐ 낮음 |
| c. 전투 진입 컷씬 정지 화면 | 미션 시작 시 잠깐 보이는 분위기 일러스트 | ⭐⭐ 중간 |
| d. 미션 결과 화면 배경 | 전투 종료 결과창의 흐릿한 배경 | ⭐⭐ 중간 |
| e. 월드맵 배경 (`TacticalScene` 외) | 월드맵 노드 그래프 위 풍경 | ⭐⭐⭐ 높음 (별도 트랙) |

### 7.2 권장 1차 트랙

- **a. 맵 외곽 보더** + **c. 전투 진입 정지 화면** 두 종부터 시작
- 외곽 보더는 지역별 1장씩 (5장 내외)
- 진입 정지 화면은 미션 분위기 표현용으로 지역×시점 조합 ~10장

> §7.3 산출 우선순위는 §3.3 팔레트 확정 후 별도 작업 단위로 분리.

---

## 8. AI 이미지 생성 프롬프트 — 공통 헤더

모든 타일·배경 생성에 공통으로 들어갈 스타일 헤더 (영문 / 한국어 정의 병기는 운용 메모용).

```
Top-down orthographic pixel art tile, flat-top hexagonal shape,
post-apocalyptic African quarantine zone, Front Mission 1 SNES tactical map tone,
matte low-saturation military aesthetic, dry earth and rusted metal,
limited 8-color palette: deep shadow #1A1814, dark olive #3D4825,
olive green #6E7335, khaki tan #A88E5A, concrete gray #8A8A86,
steel blue-gray #3E5A78, rust red #A0502E, highlight beige #D4BC8E,
SNES-era dithering allowed for face gradients (water, dust, metal sheen),
single light source from upper-left, short shadow toward lower-right,
no anti-aliasing, sharp pixel edges, transparent background outside hex shape,
seamless edges that tile naturally with neighbors,
NO text, NO UI elements, NO characters, NO vehicles
```

(*안 D · Front Mission 1 팔레트 확정 (§3.1). 향후 톤 시프트 시 §3.4 후반부 변형색 추가 사용*)

### 8.1 타일별 변수

각 타일은 위 헤더에 다음 한 줄을 더한다 (예시):

| 지형 | 추가 프롬프트 |
|---|---|
| 아스팔트 | `cracked asphalt road surface, faded white lane marks, scattered debris` |
| 진흙 | `wet mud patch with tire tracks, dark brown earth, slight wetness sheen` |
| 잔해 | `concrete and rebar rubble pile, shattered building fragments, dust` |
| 물웅덩이 | `shallow muddy water reflecting dusk sky, ripples, partly dry edges` |
| 건물 내부 | `top-down view of broken building interior, tile floor remnants, partial walls` |
| 마른 흙 | `dry cracked earth, no vegetation, faint footprints, dusty texture` |

### 8.2 프롬프트 검증 절차

1. 첫 1장 생성 → 후처리 통과 → Unity 임포트 → TerrainTestScene 1셀에 임시 배치
2. 시각 검수 후 OK → 동일 헤더로 변형 4장 일괄 생성
3. NG 시 헤더의 어떤 키워드가 원인인지 1개씩 토글하며 격리

### 8.3 샘플 1지역 일괄 생성 프롬프트 (Khemar Enclave · 3 시트)

> §6.4 카탈로그 39 SKU를 **3장 시트**로 분할 생성. 1장당 다중 타일을 격자로 배치해 톤 일관성을 확보.
> 시안 OK → §6.4.4 회전 파생 + §9 후처리 파이프라인 → 본 에셋 / 배틀씬 외곽 보더로 승격.

#### 8.3.1 시트 A · 지면 타일 16종 (4×4 격자)

```
Create a 4x4 grid sheet of top-down pixel art HEX TILES for a turn-based
tactical game set in Khemar Enclave (a fictional besieged district in
post-war East Africa, modeled on Gaza Strip-style blockade zones).

Each tile is a flat-top hexagon, 256x222 pixels, transparent background
outside the hex shape. Sheet total 1024x888 px.

Style: Front Mission 1 (SNES, 1995) tactical map tone — matte, low-saturation,
military aesthetic, 16-bit SNES dithering allowed for face gradients.

Strict 8-color palette ONLY:
#1A1814 deep shadow, #3D4825 dark olive, #6E7335 olive green,
#A88E5A khaki tan, #8A8A86 concrete gray, #3E5A78 steel blue-gray,
#A0502E rust red, #D4BC8E highlight beige.

Tile contents in reading order (left-to-right, top-to-bottom):
Row 1 (Dry Soil — base ground, 4 crack stages):
  1. Smooth dry earth, faint dust patterns
  2. Light cracks, scattered pebbles
  3. Medium cracks, sparse dry weeds
  4. Heavy cracks, exposed dry roots
Row 2 (Asphalt road — 3 stages + Mud shallow):
  5. Cracked asphalt, faded white lane marks
  6. Heavy cracked asphalt, potholes
  7. Shattered asphalt, exposed gravel
  8. Shallow muddy patch with tire tracks
Row 3 (Mud deep + Water puddle + Rubble x2):
  9. Deep wet mud, dark brown, slight sheen
  10. Shallow muddy water (steel blue-gray) reflecting dusk, slight ripples
  11. Concrete rubble pile, rebar exposed, dust
  12. Mixed concrete-and-metal rubble, twisted beams
Row 4 (Rubble metal + Building floor x3):
  13. Rusted metal debris, sheet panels, exposed rivets
  14. Tile floor remnants, broken ceramic, dust
  15. Concrete floor, cracked, dark stains
  16. Wooden plank floor, splintered, partial collapse

Style rules (apply to ALL tiles):
- Top-down orthographic, no tilt
- Single light source from upper-left, short shadow toward lower-right
- 3-tone shading per element: base + shadow + highlight
- No anti-aliasing, sharp 1-pixel edges, NO smoothing
- Seamless edges that tile naturally with neighbors of the same kind
- NO text, NO UI, NO characters, NO vehicles, NO objects on tiles
- Dithering allowed only on face interiors, never on hex outline (1 px clean)
```

#### 8.3.2 시트 B · 엄폐물 3종 × 6방향 (3×6 격자)

```
Create a 3x6 grid sheet of top-down pixel art COVER OBJECTS placed along
hexagon edges, for a turn-based tactical game (Khemar Enclave setting,
Front Mission 1 SNES tone).

Each cell shows ONE cover object positioned on ONE specific edge of an
imaginary flat-top hexagon (256x222 px hex footprint, but render only
the cover strip, ~128 px long, transparent elsewhere). Sheet 1024x1332 px
or 768x1332 px (3 cols x 6 rows). Each cell ~256 wide x ~222 tall.

Strict 8-color palette ONLY:
#1A1814, #3D4825, #6E7335, #A88E5A, #8A8A86, #3E5A78, #A0502E, #D4BC8E.

Columns (3 cover types):
- Col 1: SANDBAG ROW (small cover, 1-face, ~128 long x 24 tall)
  Stacked sandbags, khaki tan with rust red faded paint marks.
- Col 2: BROKEN CONCRETE WALL (medium cover, 3-face, ~128 long x 40 tall)
  Crumbling reinforced concrete wall segment, rebar exposed, concrete gray
  with deep shadow cracks and dust at base.
- Col 3: RUSTED CARGO CONTAINER (medium cover, 3-face, ~128 long x 40 tall)
  Half-rusted shipping container side, rust red dominant with concrete gray
  dents, faded stenciled markings (no readable text).

Rows (6 hexagon edges, flat-top orientation):
- Row 1: N edge — cover lies horizontally along the TOP of the hex
- Row 2: NE edge — cover rotated 60° clockwise, upper-right of hex
- Row 3: SE edge — cover rotated 120° clockwise, lower-right of hex
- Row 4: S edge — cover horizontal along the BOTTOM of the hex
- Row 5: SW edge — cover rotated 240° clockwise, lower-left of hex
- Row 6: NW edge — cover rotated 300° clockwise, upper-left of hex

Style rules:
- Top-down orthographic
- Light source from upper-left CONSISTENT across all cells (do not rotate
  the lighting with the cover — only the cover geometry rotates)
- Short shadow toward lower-right of the cover
- 3-tone shading: base + shadow + highlight
- No anti-aliasing, sharp pixel edges
- Transparent background outside the cover strip
- All cover objects shown at FULL HP (intact 3-face state for medium covers)
- NO text, NO UI, NO characters, NO ground tile beneath
```

#### 8.3.3 시트 C · 차체 스케일 + 풍경 오브젝트 5종 (1+4 배치)

```
Create a reference sheet showing 1 vehicle and 4 prop objects for a top-down
turn-based tactical game (Khemar Enclave setting, Front Mission 1 SNES tone).
Layout: large vehicle on the LEFT (occupying ~half the sheet), 2x2 grid of
props on the RIGHT. Sheet 1024x768 px, transparent background.

Strict 8-color palette ONLY:
#1A1814, #3D4825, #6E7335, #A88E5A, #8A8A86, #3E5A78, #A0502E, #D4BC8E.

LEFT — Vehicle "Rocinante" (Assault medium tank, top-down):
- Approx 160 px long x 96 px wide, fits inside a 256x222 hex with ~30 px
  rotation margin
- Boxy, hand-built improvised tank silhouette (post-apocalyptic salvage
  aesthetic, NOT military regulation). Welded plate armor, asymmetric panels,
  exposed bolts, mismatched rust patches
- Main body: olive green base with dark olive shadow side
- Turret centered, slightly off-rear, with one main cannon pointing UP (north)
- Hatches, antennas, equipment box on rear deck
- Faded numbering or hand-painted mark in highlight beige (no readable text)
- Wear: rust red streaks, concrete gray dust at lower edges
- Render at FULL HP (no damage)

RIGHT — 2x2 grid of props (each ~128x128 px, top-down):
- Top-left: Dead leafless tree, twisted trunk, deep shadow base
- Top-right: Stone-rim well with wooden bucket beside, slight water glint
  (steel blue-gray) inside
- Bottom-left: Rusted fuel drum (rust red with concrete gray bands), tipped
  slightly, small spill stain
- Bottom-right: Debris pile (mixed broken wooden planks, rebar, brick
  fragments)

Style rules:
- Top-down orthographic, no tilt
- Single light source from upper-left, short shadow toward lower-right
- 3-tone shading: base + shadow + highlight per surface
- No anti-aliasing, sharp pixel edges
- Transparent background, no ground tile
- NO text on any object, NO UI, NO characters, NO weapons firing
- All objects at full intact state
```

#### 8.3.4 검증 체크리스트 (시안 OK/NG 판정)

| # | 점검 항목 | OK 기준 |
|---|---|---|
| 1 | 팔레트 준수 | §3.1 8색 외 색이 면적 0.5% 이상 차지 X |
| 2 | 채도 | 모든 색 채도 50% 이하 (액센트 제외) |
| 3 | 안티에일리어싱 | 가장자리에 0.5 px 회색 보간 X |
| 4 | 라이팅 일관 | 모든 셀에서 좌상단→우하단 광원 동일 |
| 5 | 디더링 | 면 내부에만 적용. 헥스 외곽 1 px 깔끔 |
| 6 | 차체 스케일 | Assault 본체 길이가 헥스 가로의 60~65% (160 / 256) |
| 7 | 엄폐물 방향 | 6 방향 모두 60° 단위로 정렬, 광원 회전 X |
| 8 | 시임리스 | 동일 카테고리 4장 격자 인접 시 변 색·텍스처 자연스러움 |

> 1~3 위반 시 후처리(팔레트 양자화·점 필터 다운샘플)로 회복 가능.
> 4~7 위반 시 프롬프트 재생성. 광원 일관·방향 정렬은 사후 회복 매우 어려움.

---

## 9. 후처리 파이프라인

| 단계 | 도구 | 작업 |
|---|---|---|
| 1. 다운샘플 | Photoshop / Aseprite | 1024+ 소스 → 256×222 px |
| 2. 팔레트 양자화 | Aseprite (Indexed Color) | §3 확정 팔레트로 강제 매핑 |
| 3. 헥스 마스킹 | Photoshop 알파 마스크 | 헥스 형태 외부 알파 0 |
| 4. 시임리스 검증 | Aseprite 7x7 미리보기 | 인접 6방향 타일과 색·라인 자연스러움 |
| 5. PNG 저장 | Aseprite Export | PNG-32, 압축 없음 |
| 6. Unity 임포트 | Unity Editor | §10 설정 적용 |

### 9.1 자동화 후보

- 1~5단계는 Aseprite 스크립트 또는 Photoshop 액션으로 일괄화 가능
- 별도 트랙으로 자동화 스크립트 작성 검토 (사용자 결정)

---

## 10. Unity 임포트 설정

| 항목 | 값 | 비고 |
|---|---|---|
| Texture Type | Sprite (2D and UI) | |
| Sprite Mode | Single | 타일 1장당 PNG 1개 |
| Pixels Per Unit | **128** | size=128 헥스에 1셀 1유닛 정렬 가정 (실제 값은 dev 측 GameConstants 확인) |
| Mesh Type | Tight | 헥스 마스크 외 알파 영역 컬링 |
| Filter Mode | **Point (no filter)** | 픽셀 아트 필수 |
| Compression | **None** | 픽셀 보전 |
| Generate Mip Maps | Off | 톱다운 카메라 거리 일정 |
| Wrap Mode | Clamp | |
| Read/Write | Off | 메모리 절약 |

### 10.1 디렉토리 구조 (제안)

```
Assets/_Project/Sprites/
├── Tiles/
│   ├── Asphalt/
│   │   ├── asphalt_01.png
│   │   ├── asphalt_02.png
│   │   └── asphalt_03.png
│   ├── Mud/
│   ├── Rubble/
│   ├── Water/
│   ├── Building/
│   └── Soil/
├── Battlefield/
│   ├── Borders/
│   │   └── (지역별)
│   └── Intros/
└── _Masks/
    └── hex_mask_256x222.png
```

> dev 측 현행 디렉토리와 차이 시 dev `Crux-dev/CLAUDE.md` 우선. 본 항목은 권장안.

---

## 11. 결정 필요 항목

| # | 항목 | 사용자 검토 |
|---|---|---|
| 1 | §2 시각 스타일 — A 픽셀 아트 / B 페인터리 / C 하이브리드 | A 권장, 확정 필요 |
| 2 | §3 컬러 팔레트 | ✅ 확정 — 안 D · Front Mission 1 (2026-04-29) |
| 3 | §5 타일 크기 — 256×222 vs 128×111 vs 다른 값 | dev 측 현재 그리드 셀 크기와 정합 확인 필요 |
| 4 | §6.4 샘플 1지역 SKU (현 39종 — 지면 16 + 엄폐 18 + 풍경 4 + 차체 1) | 합격 시 본 에셋 승격, 지역 추가 결정 |
| 5 | §7 베틀씬 배경 1차 트랙 — 외곽 보더 + 진입 정지 화면 | 순서·우선도. §6.4 시안 합격 시 동일 톤 재활용 |
| 6 | §8 생성 경로 — 외부 ChatGPT vs Coplay MCP `generate_or_edit_images` | 두 경로 병행 가능. 1차 시도 경로 선택 |
| 7 | §6.4.3 차체·엄폐물 픽셀 비례 (Assault 160×96, 엄폐 128×24/40) | dev `GameConstants` PPU·hex size와 ±10% 정합 확인 후 확정 |
| 8 | §10 Unity 임포트 PPU — 128 가정 / dev 측 실측 값 | dev 워크트리에서 확인 후 확정 |
| 9 | 후처리 자동화 스크립트 작성 여부 | Aseprite 스크립팅 기여 가능 |

---

## 12. 외부 문서 연결

| 문서 | 연결 지점 |
|---|---|
| `docs/02 v2.0` | §1 톤·무드의 모든 정서적 근거 |
| `docs/06 §8` | §6 지형 타일 카탈로그의 6종 출처 |
| `docs/09 §1.4` | §6.2 지역 변형의 분포 |
| `docs/10 UI` | UI 위젯·아이콘 시각 톤 — 본 가이드와 팔레트 공유 |
| `Crux-dev/CLAUDE.md TD-06` | "지형 플로어 스프라이트 다크톤 multiply 틴트 약함" — 본 가이드 산출물로 대체 예정 |

---

## 13. 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-04-29 | 초판 v0.1 — 톤·스타일 결정안·팔레트 3안·헥스 기하·타일 카탈로그 1차·AI 프롬프트 공통 헤더·후처리 파이프라인·Unity 임포트 사양·결정 필요 항목 8건 작성. §2·§3·§7 사용자 확정 대기. |
| 2026-04-29 | v0.2 — Front Mission 1 (SNES 1995, Squaresoft) 레퍼런스 채택. 안 A/B/C 폐기 후 안 D 8색 베이스(#1A1814~#D4BC8E) 확정 (§3.1). §2.1 디더링 정책 SNES 16비트 톤 허용으로 완화. §3.4 후반부 회복 톤 시프트 추가. §8 AI 프롬프트 공통 헤더 hex 코드 안 D로 교체. §11 #2 결정 필요 항목 ✅ 확정 마킹. |
| 2026-04-29 | v0.2.1 — §3.5 GPT 생성 프롬프트 초안 추가 (팔레트 시안 1장 + 단일 타일 1장 + 3단계 사용 절차). 외부 이미지 모델로 참고 시안 빠르게 뽑기 위한 복붙용. |
| 2026-04-29 | v0.3 — §6.4 샘플 1지역(케마르 격리구·1막 무대) SKU 카탈로그 39종 + 스케일 비례(Assault 160×96, 엄폐 128×24/40) + 6변 방향 규약(N/NE/SE/S/SW/NW × 60° 회전 파생). §8.3 일괄 생성 프롬프트 3시트(지면 16 + 엄폐물 18 + 차체+풍경 5) + 시안 OK/NG 8 체크. §11 결정 필요 항목 8→9건(차체 스케일 정합 신설). 시안 합격 시 배틀씬 외곽 보더로 승격 가능. |
