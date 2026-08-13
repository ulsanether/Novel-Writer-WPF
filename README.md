# Claude에 Novel Writer WPF 개발 

Novel Writer (WPF 기반 소설 작가용 집중형 에디터)

---

## 목차
1. [프롬프트 구조](#프롬프트-구조)
2. [Phase 1: 초기 프로젝트 설정](#phase-1-초기-프로젝트-설정)
3. [Phase 2: 집중 모드 에디터](#phase-2-집중-모드-에디터)
4. [Phase 3: 저장 및 관리](#phase-3-저장-및-관리)
5. [Phase 4: 테마 및 설정](#phase-4-테마-및-설정)
6. [효과적한 요청 팁](#효과적한-요청-팁)
7. [체크리스트](#체크리스트)

---

## 프롬프트 구조

효과적한 Claude 요청은 다음 구조를 따릅니다:

```
1. 프로젝트 개요 (What)      - 무엇을 만드는가?
2. 기술 요구사항 (How)       - 어떻게 만드는가?
3. 기능 목록 (Features)       - 어떤 기능이 필요한가?
4. 구조 (Architecture)        - 파일 구조와 클래스 다이어그램
5. 단계별 목표 (Phases)       - 단계별로 무엇을 할 것인가?
6. 제약사항 (Constraints)     - 제한사항과 선호도
```

---

## Phase 1: 초기 프로젝트 설정

### 📋 사용할 프롬프트

```markdown
# Novel Writer - WPF 데스크톱 앱 초기 프로젝트 설정

## 프로젝트 개요
소설 작가를 위한 집중형 워드프로세서 데스크톱 애플리케이션을 만들어야 합니다.
FocusWriter (https://codeberg.org/gottcode/focuswriter) 에서 영감을 얻었으며, 
Windows WPF + MVVM 아키텍처로 구현해야 합니다.

## 기술 요구사항
- **프레임워크**: WPF (Windows Presentation Foundation), .NET 6 이상
- **아키텍처 패턴**: MVVM (Model-View-ViewModel)
- **라이브러리**: 
  - Microsoft.Toolkit.Mvvm (MVVM 패턴)
  - DocumentFormat.OpenXml (DOCX 처리)
  - System.Data.SQLite (로컬 데이터베이스)
- **언어**: C#
- **플랫폼**: Windows 데스크톱

## 최소 기능 요구사항
1. 집중 모드: 전체화면, 메뉴/UI 자동 숨김 (마우스 이동시 표시)
2. 텍스트 에디팅: 기본 입력/삭제, 실시간 통계
3. 통계: 단어수, 문자수, 페이지수, 단락수, 문장수
4. 저장: SQLite 기반 로컬 저장, 자동 저장, 백업
5. 내보내기: DOCX 형식 저장
6. 테마: 다크모드, 라이트모드, 커스텀 테마
7. 다국어: 한글, 영어 지원
8. 타이머: 목표 시간 설정 및 카운트다운
9. 일일 목표: 단어수 목표 설정 및 진행률 표시
10. 한글 단어 오류 검사.
11. ai로 문서 작성 오타 수정. 

## 코드 스타일 요구사항
- MVVM Toolkit의 ObservableObject, RelayCommand 사용
- partial 클래스 + [ObservableProperty] 속성 사용
- 비동기 작업은 async/await 사용
- XML 문서 주석 추가 (요약, 파라미터, 반환값)
- 한글 주석 사용
- 인덴트: 4칸 (탭 사용 금지)

## 출력 형식
각 파일을 별도의 코드 블록으로 제공해주세요.
파일명과 전체 코드를 포함해주세요.


## 코드 스타일 요구사항
- MVVM Toolkit의 ObservableObject, RelayCommand 사용
- partial 클래스 + [ObservableProperty] 속성 사용
- 비동기 작업은 async/await 사용
- XML 문서 주석 추가 (요약, 파라미터, 반환값)
- 한글 주석 사용
- 인덴트: 4칸 (탭 사용 금지)

## 출력 형식
각 파일을 별도의 코드 블록으로 제공해주세요.
파일명과 전체 코드를 포함해주세요.


