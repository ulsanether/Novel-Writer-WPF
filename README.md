

#Novel Writer


<p align="center">
  <img src="Img/01.png" width="30%" style="margin: 5px;">
  <img src="Img/02.png" width="30%" style="margin: 5px;"><br>
  <img src="Img/03.png" width="30%" style="margin: 5px;">
  <img src="Img/04.png" width="30%" style="margin: 5px;"><br>
  <img src="Img/05.png" width="30%" style="margin: 5px;">
  <img src="Img/06.png" width="30%" style="margin: 5px;"><br>
  <img src="Img/07.png" width="30%" style="margin: 5px;">
</p>




## Phase 1: 초기 프로젝트 설정


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


