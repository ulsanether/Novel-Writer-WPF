# 이미지 생성 UI 설정 설계안

## 1. 목적

이미지 생성 프로그램에서 사용자가 프롬프트 문법을 직접 알지 못해도  
화풍, 품질, 조명, 색감, 구도, 카메라, 인물, 분위기, 콘텐츠 등급 등을 직관적으로 제어할 수 있도록 UI를 구성한다.

기본 사용자는 프리셋 중심으로 빠르게 이미지를 생성하고,  
고급 사용자는 세부 옵션과 생성 파라미터를 직접 조정할 수 있는 구조를 권장한다.

---

# 2. 권장 UI 구조

## 상단: 프리셋

빠른 선택용 대표 스타일 프리셋.

- 실사
- 시네마틱
- 애니메이션
- 일러스트
- 3D
- 콘셉트 아트
- 수채화
- 유화
- 픽셀 아트
- 사용자 저장 프리셋

---

# 3. 세부 설정 카테고리

## 3.1 Style — 화풍

### 스타일 종류

- 포토리얼
- 시네마틱
- 애니메이션
- 만화
- 일러스트
- 콘셉트 아트
- 수채화
- 유화
- 아크릴화
- 연필화
- 잉크 드로잉
- 3D 렌더
- 클레이
- 픽셀 아트
- 레트로
- 판타지
- SF
- 미니멀
- 사용자 지정

### 스타일 강도

슬라이더 권장.

`Subtle 0 ───────────── 100 Strong`

설명:

- 낮음: 원본 표현을 최대한 유지
- 중간: 선택한 스타일이 자연스럽게 반영
- 높음: 선택한 화풍을 강하게 적용

---

# 4. Quality — 품질

## 생성 품질

- 빠른 생성
- 표준
- 고품질
- 최고품질

## 디테일

슬라이더:

`Simple 0 ───────────── 100 Detailed`

## 해상도

예:

- 512 × 512
- 768 × 768
- 1024 × 1024
- 1024 × 1536
- 1536 × 1024
- 사용자 지정

## 업스케일

- 사용 안 함
- 2×
- 4×

---

# 5. Composition — 구도

## 촬영 범위

- 얼굴 클로즈업
- 클로즈업
- 상반신
- 무릎 위
- 전신
- 와이드샷
- 익스트림 와이드샷

## 구도 방식

- 중앙 구도
- 삼분할
- 대칭
- 비대칭
- 여백 강조
- 피사체 좌측
- 피사체 우측
- 역동적 구도
- 미니멀 구도

## 배경 복잡도

슬라이더:

`Minimal 0 ───────────── 100 Complex`

---

# 6. Camera — 카메라

## 카메라 각도

- 정면
- 측면
- 3/4 뷰
- 하이앵글
- 로우앵글
- 오버헤드
- 탑뷰
- 바닥 시점
- 1인칭
- 드론 시점

## 렌즈

- 16mm
- 24mm
- 35mm
- 50mm
- 85mm
- 105mm
- 135mm
- 200mm
- 매크로
- 어안
- 자동

## 피사계 심도

슬라이더:

`Deep Focus 0 ───────────── 100 Strong Bokeh`

---

# 7. Lighting — 조명

## 조명 종류

- 자연광
- 부드러운 자연광
- 스튜디오
- 소프트박스
- 하드 라이트
- 시네마틱
- 역광
- 림라이트
- 네온
- 골든아워
- 블루아워
- 야간 조명
- 촛불
- 창문광
- 볼류메트릭 라이트

## 광원 방향

- 정면
- 왼쪽
- 오른쪽
- 뒤쪽
- 위쪽
- 아래쪽
- 자동

## 조명 대비

슬라이더:

`Soft 0 ───────────── 100 Dramatic`

## 밝기

슬라이더:

`Dark 0 ───────────── 100 Bright`

---

# 8. Color — 색감

## 색상 스타일

- 자연색
- 따뜻한 색감
- 차가운 색감
- 파스텔
- 비비드
- 저채도
- 모노톤
- 흑백
- 세피아
- 영화 색보정
- 빈티지 필름

## 색온도

슬라이더:

`Cool 0 ───────────── 100 Warm`

## 채도

슬라이더:

`Muted 0 ───────────── 100 Vibrant`

## 대비

슬라이더:

`Flat 0 ───────────── 100 High Contrast`

---

# 9. Mood — 분위기

- 밝음
- 따뜻함
- 평화로움
- 자연스러움
- 몽환적
- 신비로움
- 우울함
- 어두움
- 공포
- 긴장감
- 로맨틱
- 감성적
- 웅장함
- 미래적
- 고독
- 행복
- 차분함

---

# 10. Texture — 질감

## 표면 및 이미지 질감

- 매끈함
- 자연스러움
- 거친 질감
- 필름 그레인
- 종이 질감
- 캔버스
- 페인터리
- 디지털 클린
- 빈티지 필름
- 고해상도 질감

## 질감 강도

슬라이더:

`Clean 0 ───────────── 100 Textured`

---

# 11. Realism — 현실감

슬라이더:

`Stylized 0 ───────────── 100 Photoreal`

설명:

- 0~30: 강한 스타일 표현
- 30~70: 세미리얼
- 70~100: 실사 중심

---

# 12. Character — 인물

## 인물 수

- 없음
- 1명
- 2명
- 3명
- 다수
- 사용자 지정

## 등장인물 연령대

콘텐츠 등급과 반드시 별도로 관리한다.

- 영아
- 유아
- 어린이
- 청소년
- 20대
- 30대
- 40대
- 50대
- 60대 이상
- 특정 연령 지정

## 표정

- 자연스러움
- 무표정
- 미소
- 웃음
- 행복
- 슬픔
- 분노
- 놀람
- 긴장
- 진지함
- 사용자 지정

## 포즈

- 자동
- 자연스러운 자세
- 정면
- 서 있음
- 앉아 있음
- 걷기
- 달리기
- 역동적
- 패션 포즈
- 사용자 지정

## 시선

- 카메라 응시
- 카메라 밖
- 왼쪽
- 오른쪽
- 위
- 아래
- 피사체 응시

---

# 13. Environment — 환경

## 배경

- 없음
- 투명
- 단색
- 스튜디오
- 실내
- 집
- 카페
- 사무실
- 도시
- 거리
- 자연
- 숲
- 산
- 바다
- 우주
- 판타지
- 사용자 지정

## 시간대

- 새벽
- 아침
- 낮
- 오후
- 골든아워
- 일몰
- 저녁
- 밤
- 심야

## 날씨

- 맑음
- 흐림
- 비
- 폭우
- 눈
- 안개
- 폭풍
- 노을
- 사용자 지정

---

# 14. Content — 콘텐츠 설정

## 콘텐츠 이용 등급

등장인물 연령과 별도 설정.

- 전체 이용가
- 12+
- 15+
- 18+

## 폭력 표현 강도

- 없음
- 약함
- 중간
- 강함

## 선정성 표현 강도

- 없음
- 약함
- 중간
- 성인 콘텐츠

## 공포 강도

- 없음
- 약함
- 중간
- 강함

## 기타 제한

- 욕설 표현 제외
- 무기 표현 제외
- 혈액 표현 제외
- 혐오 이미지 제외
- 성인 콘텐츠 제외

---

# 15. Post Processing — 후처리

옵션:

- HDR
- Bloom
- Vignette
- Film Grain
- Chromatic Aberration
- Lens Flare
- Sharpen
- Soft Focus
- Glow
- Motion Blur
- Depth Blur
- Color Grading
- Film Look

각 옵션은 체크박스 + 강도 슬라이더 형태를 권장한다.

---

# 16. 핵심 슬라이더 권장 구성

UI 공간이 제한적이라면 아래 8개를 우선 제공한다.

1. 스타일 강도  
   `Subtle ↔ Strong`

2. 현실감  
   `Stylized ↔ Photoreal`

3. 디테일  
   `Simple ↔ Detailed`

4. 조명 대비  
   `Soft ↔ Dramatic`

5. 채도  
   `Muted ↔ Vibrant`

6. 색온도  
   `Cool ↔ Warm`

7. 배경 복잡도  
   `Minimal ↔ Complex`

8. 피사계 심도  
   `Deep Focus ↔ Strong Bokeh`

---

# 17. Advanced — 고급 설정

고급 사용자를 위한 영역으로 기본 상태에서는 접어두는 것을 권장한다.

## 모델

- Model
- Checkpoint
- VAE

## 생성 설정

- Seed
- Steps
- CFG / Guidance
- Sampler
- Scheduler
- Denoise Strength

## LoRA

- LoRA 선택
- 다중 LoRA
- LoRA Weight

## Reference Image

- Reference Image 추가
- Reference Strength
- Style Reference
- Character Reference
- Pose Reference
- Composition Reference

## ControlNet

- Pose
- Depth
- Edge
- Lineart
- Normal
- Scribble

## 후처리

- Face Detail
- Face Restore
- Upscaler
- High Resolution Fix

## Prompt

- Positive Prompt
- Negative Prompt
- Prompt Weight
- Prompt Preview

---

# 18. 권장 메뉴 순서

좌측 사이드 패널 기준:

1. Preset
2. Style
3. Quality
4. Composition
5. Camera
6. Lighting
7. Color
8. Mood
9. Texture
10. Character
11. Environment
12. Content
13. Post Processing
14. Advanced

---

# 19. 권장 UX 구조

## Basic Mode

초보 사용자용.

노출 항목:

- Prompt
- Preset
- Style
- Quality
- Aspect Ratio
- Lighting
- Color
- Generate

## Pro Mode

숙련 사용자용.

추가 노출:

- Composition
- Camera
- Lens
- DOF
- Mood
- Texture
- Character
- Environment
- Content
- Post Processing
- Seed
- Steps
- CFG
- Sampler
- Scheduler
- LoRA
- ControlNet
- Reference Image

---

# 20. Prompt 자동 변환 구조

사용자는 기술적인 프롬프트 문법을 직접 입력하지 않아도 된다.

예:

UI 설정:

- Style: Cinematic
- Camera: 85mm
- Lighting: Soft cinematic
- Color: Warm
- Depth of Field: Strong
- Quality: High

내부 Prompt 예시:

```text
cinematic photography,
85mm lens,
soft cinematic lighting,
warm color grading,
shallow depth of field,
high detail,
professional photography
```

사용자 UI와 실제 Prompt 생성 엔진을 분리하는 구조를 권장한다.

---

# 21. 연령 관련 설계 원칙

다음 두 요소는 절대로 하나의 옵션으로 통합하지 않는다.

## 등장인물 연령

이미지에 등장하는 사람의 나이.

예:

- 어린이
- 청소년
- 20대
- 30대
- 중년
- 노년

## 콘텐츠 이용 등급

이미지가 표현할 수 있는 콘텐츠 수위.

예:

- 전체 이용가
- 12+
- 15+
- 18+

데이터 구조에서도 별도 필드로 관리하는 것을 권장한다.

예:

```json
{
  "character_age": "30s",
  "content_rating": "15+"
}
```

---

# 22. 추천 데이터 구조

예:

```json
{
  "preset": "cinematic",
  "style": {
    "type": "photoreal",
    "strength": 70,
    "realism": 90
  },
  "quality": {
    "level": "high",
    "detail": 85,
    "resolution": "1024x1536"
  },
  "composition": {
    "shot": "full_body",
    "layout": "rule_of_thirds",
    "background_complexity": 40
  },
  "camera": {
    "angle": "eye_level",
    "lens": "85mm",
    "depth_of_field": 75
  },
  "lighting": {
    "type": "cinematic",
    "direction": "left",
    "contrast": 65,
    "brightness": 55
  },
  "color": {
    "preset": "warm",
    "temperature": 70,
    "saturation": 55,
    "contrast": 60
  },
  "mood": "calm",
  "character": {
    "count": 1,
    "age": "30s",
    "expression": "natural",
    "pose": "standing"
  },
  "environment": {
    "background": "city",
    "time": "golden_hour",
    "weather": "clear"
  },
  "content": {
    "rating": "all",
    "violence": "none",
    "sexual_content": "none",
    "horror": "none"
  }
}
```

---

# 23. 최종 권장 구조

전체 UI는 다음 원칙으로 구성한다.

**초보 사용자**

`프롬프트 → 프리셋 → 몇 가지 핵심 옵션 → 생성`

**숙련 사용자**

`프롬프트 → 스타일 → 구도 → 카메라 → 조명 → 색감 → 인물 → 환경 → 콘텐츠 → 고급 설정 → 생성`

가장 중요한 것은 많은 옵션을 처음부터 모두 보여주는 것이 아니라  
기본 옵션과 고급 옵션을 단계적으로 분리하는 것이다.

사용자가 자연어 프롬프트와 UI 옵션을 함께 사용하고,  
내부 시스템이 이를 실제 이미지 모델용 Prompt와 생성 파라미터로 변환하는 구조를 권장한다.
