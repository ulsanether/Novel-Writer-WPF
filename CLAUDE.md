# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

FocusWriter에서 영감을 받은, 소설 작가용 집중형 워드프로세서 데스크톱 앱입니다. WPF + MVVM(CommunityToolkit.Mvvm)으로 구현되며 **Windows 전용**(`net8.0-windows`, `UseWPF`)입니다.

## 명령어

빌드/실행은 리포지토리 루트에서 수행합니다. 솔루션 파일은 신형 `.slnx`(`NovelWriter.slnx`)이며 프로젝트는 `NovelWriter.Wpf/` 하나뿐입니다.

```bash
dotnet build                              # 빌드
dotnet run --project NovelWriter.Wpf      # 실행 (Windows에서만 동작)
```

- 프로젝트/네임스페이스는 `NovelWriter.Wpf`이지만, **어셈블리(exe) 이름은 `NovelWriter`**(`<AssemblyName>`)입니다 — 산출물은 `bin/Debug/net8.0-windows/NovelWriter.exe`. 앱 아이콘은 `Resources/app.ico`(`<ApplicationIcon>`).

- 테스트 프로젝트는 아직 없습니다. 테스트 추가 시 별도 xUnit/NUnit 프로젝트를 만들고 `.slnx`에 등록해야 합니다.
- Windows가 아닌 환경에서 빌드만 하려면 `EnableWindowsTargeting`이 이미 켜져 있어 `dotnet build`는 통과하지만 실행은 불가합니다.

## 아키텍처

전형적인 MVVM이되 몇 가지 비관용적(non-obvious) 결정이 있으므로 주의:

- **DI 컨테이너 없음.** 모든 서비스와 `MainViewModel`은 `MainWindow.xaml.cs` 생성자에서 **수동으로 new & 조립**됩니다(`App.xaml.cs`는 비어 있음). 새 서비스를 추가하려면 이 생성자와 `MainViewModel`의 생성자 파라미터를 함께 수정해야 합니다.
- **View → ViewModel 콜백 주입.** 파일 열기/저장 다이얼로그는 View의 관심사이므로, `MainViewModel.ImportPathResolver` / `ExportPathResolver` (`Func<Task<string?>>`)를 `MainWindow`에서 설정해 주입합니다. ViewModel은 `Microsoft.Win32` 다이얼로그를 직접 참조하지 않습니다.
- **단일 문서 저장소.** `DocumentRepository`(SQLite)는 다중 문서를 저장하지 않습니다. `SaveAsync`가 매번 `DELETE FROM Documents` 후 INSERT 하므로 **항상 문서 1개만** 유지됩니다. 이건 프로젝트 미사용 시의 기본 자동저장 백엔드입니다(아래 작품 프로젝트가 통합 저장을 담당).
- **작품 프로젝트(`.novel`) 통합 관리.** `Models/NovelProject`(+`ProjectAiSettings`/`ProjectImageSettings`)가 **원고(평문)·스토리 설계(`StoryProject`)·AI(모델/BaseUrl)·이미지(ComfyUI 주소/경로/체크포인트/하드웨어) 설정을 한 개의 `.novel`(JSON) 파일**에 통합. `NovelProjectService`(확장자 `.novel`)의 `CreateAsync`(작품명 폴더 + 유형별 하위폴더 `Characters/Illustrations/Backgrounds/...` 생성 후 `.novel` 저장)·`SaveAsync`·`Load`(+`CurrentPath`/`CurrentFolder`). 상단 **"작품" 메뉴**(새 작품/열기/저장, `NewProject`/`OpenProject`/`SaveProject` 명령). 새 작품/열기는 `NewProjectResolver`(SaveFileDialog로 위치+제목=파일명 동시 지정)·`OpenProjectResolver`(OpenFileDialog) 콜백(`MainWindow`). `MainViewModel.ApplyProject`가 프로젝트를 편집 상태에 반영(제목→창 제목 `AppTitle`, 원고→`Content`, AI/이미지 설정→서비스, **참고자료 폴더=작품 폴더**), `SaveProject`가 현재 상태를 다시 수집해 저장. **스토리 플래너는 `CurrentProject.Story`(같은 인스턴스)를 편집**하므로 작품 저장 시 함께 저장됨(프로젝트 없으면 기존 `story_project.json` 폴백). **주의**: 프로젝트 없이 "작품 저장" 시 새 작품 생성이 편집기를 비우므로 `SaveProjectAsync`가 현재 원고를 보존 후 복원함. 자동저장(SQLite)은 프로젝트와 별개로 계속 동작(작품 파일은 수동 저장). **닫기 전 저장 확인**: `MainViewModel.IsProjectDirty`(원고·제목·화풍 편집 시 `MarkProjectDirty`, 스토리 플래너 닫힘 시에도 표시; `_suppressDirty`로 로드/적용 중 억제, 저장/`ApplyProject` 후 클리어). `MainWindow.Window_OnClosing`이 dirty면 Yes/No/취소 `MessageBox`(Yes→`SaveProjectCommand.ExecuteAsync`, 취소→`e.Cancel`, `_forceClose`로 재진입 방지).
- **로컬라이제이션은 resx가 아님.** `LocalizationService`가 ko/en 딕셔너리를 들고 있고, `MainViewModel`이 `XxxText` 형태의 계산 속성으로 노출합니다. 언어 전환 시 `RaiseLocalizedPropertiesChanged()`가 **모든 로컬라이즈 속성에 대해 수동으로** `OnPropertyChanged`를 호출합니다 — 새 UI 텍스트를 추가하면 (1) 두 딕셔너리, (2) ViewModel 계산 속성, (3) `RaiseLocalizedPropertiesChanged()` 목록 세 곳을 모두 갱신해야 합니다.
- **집중 모드.** `IsFocusMode` 변경을 `MainWindow`가 `PropertyChanged`로 감지해 전체화면/무테두리/Topmost로 전환합니다. 상단 크롬(`ChromePanel`)은 `Window_MouseMove` + `DispatcherTimer`(3초)로 표시/자동 숨김됩니다.

### 스토리 플래너 (계층형 스토리 설계)

`AI_시놉시스_전체_설계.md`를 구현한 별도 창(`StoryPlannerWindow`, 메뉴/툴바에서 진입). **Story Bible → 시놉시스 → 장 → Scene → 본문**을 각각 **별도 AI 호출**로 생성합니다(한 프롬프트로 전부 생성하지 않음).

- **데이터**: `Models/StoryModels.cs`(`StoryProject`/`ChapterNode`/`SceneNode`/`StoryCharacter`, 모두 `ObservableObject`). `StoryProjectService`가 `%LocalAppData%/NovelWriter/story_project.json`에 저장 — **DOCX 원고와 완전히 분리**됩니다.
- **AI**(`StoryPlannerService`, `ChatService` 재사용): `GenerateSynopsisAsync`(본문 X, 큰 사건만), `GenerateChaptersAsync`/`GenerateScenesAsync`(JSON 배열 파싱; **극적 구조 반영** — `PhaseForIndex`가 장 위치로 발단/전개/위기/절정/결말을 계산해 `ChapterNode.Phase`에 확정하고 프롬프트에 장별 단계 배치 계획을 넣음. Scene/본문 생성 프롬프트에 `PhaseGuidance`로 단계별 서술 지침 주입 → "발단 장인데 절정처럼" 같은 구조 불일치 방지. 역분석 경로는 `ApplyChapters`에서 `Phase` 미설정 시 위치로 부여. UI: TreeView 장 라벨 단계 배지 + 장 편집 패널 단계 표시), `GenerateSceneContentAsync`(본문 1패스), `GenerateSceneContentDetailedAsync`("상세 작성" — `GenerateSceneBeatsAsync`로 Scene을 4~6개 비트로 나눈 뒤 각 비트를 여러 문단으로 작성·이어붙여 **긴 본문** 생성, 진행률 보고), `CheckConsistencyAsync`(일관성 경고). 본문 생성은 **대사 비율**(`DialogueRatio` 0~100, 슬라이더)을 받아 `DialogueStyleInstruction`으로 문체 지시(0=묘사만, 100=대사만)를 프롬프트에 넣습니다. **본문 수위(성인) 설정**: `StoryProject.ContentRating`(등급) + `SexualRatio`/`ViolenceRatio`(**본문에서 차지하는 비율 0~100% 슬라이더** — 선정성 100%면 본문 전체가 성적 장면)를 **무검열 모델일 때만**(`StoryPlannerService.IsUncensoredModel`, `ModelBadgeKeywords.Uncensored`로 판별) UI 노출·프롬프트 반영. `BuildMaturityInstruction`이 등급·선정성·폭력 수위를 지시문으로 만들어 **장 생성·씬 분할·비트 분해·본문 생성** 전부에 주입(`MaturitySection` → `GenerateChaptersAsync`/`GenerateScenesAsync`/`GenerateSceneBeatsAsync`/`GenerateSceneContentAsync`/`GenerateSceneContentDetailedAsync`) — 씬 분할 단계부터 수위가 반영돼야 본문에서 제대로 표현됨. UI는 **기본 탭(작품 설정)** 의 "🔞 표현 수위" 구역(`IsUncensoredModel`로 Visibility)에 있어 씬 분할 전에 설정. 18+는 "성인 전용·수위 제한 없음" + 선정성 '노골적' 시 노골적 성애 묘사를 강제. 검열 모델엔 빈 지시(거부·왜곡 방지). UI는 씬 편집 대사비율 아래 "🔞 본문 수위" 구역(`IsUncensoredModel`로 Visibility). 로컬 모델이 Scene 설정 항목("제목/목표/등장인물/장소/갈등/결과")을 본문에 그대로 나열하는 문제는 (1) 프롬프트 강조 + (2) 후처리 `StripSceneMeta`(`SceneMetaLineRegex`로 라벨 줄 제거)로 방어합니다. **컨텍스트 최소화**: 각 호출에 Story Bible + 시놉시스 요약 + 현재 장/이전 Scene 요약만 전달(로컬 7B 모델용). 로컬 모델의 불완전 JSON은 `[`~`]` 추출 + try/catch로 방어.
- **UI**: 좌(작품 설정+시놉시스, **세로 탭 2열** — `TabControl TabStripPlacement=Left` + `ItemsPanel`=`UniformGrid Columns=2`, 탭: 기본/**배경·인물·시놉시스(병합)**/참고자료. **전체 시놉시스 생성** 버튼은 탭 밖(좌측 열 상단 `DockPanel.Dock=Top`), **장 구성 생성** 버튼은 중앙 "구조" 패널 상단으로 이동) / 중(장→Scene `TreeView`, `HierarchicalDataTemplate`, 장 라벨에 `Phase` 배지 `StringToVisibilityConverter`, 상단에 "장 구성 생성" 버튼) / 우(선택 상세 편집, 장 편집에 이야기 단계 표시). "본문 작성" 결과는 "에디터에 삽입"(`InsertToEditor` 콜백)으로 메인 `Content`에 append. 이 창은 한국어 UI 하드코딩(다국어화는 이후).
- **작품 설정 입력**: 장르·시대·세계관·결말은 **편집형 ComboBox**(선택 또는 직접 입력)이며 `StoryPlannerViewModel`의 `GenreSamples`/`EraSamples`/`WorldSamples`/`EndingSamples`(각 10개 이상)를 제공합니다.
- **원고 역분석(원고 → 설계)**: 창 **하단에 별도 구역**("📖 원고에서 역분석")으로 분리 — `GetManuscript` 콜백(메인 `Content`)을 AI로 분석해 설계를 채웁니다. 출력이 영어로 새는 것을 막기 위해 (1) 모든 생성/추출 프롬프트의 system에 "JSON 값은 반드시 한국어로" 지시, (2) **후처리 재번역**: `EnsureKoreanAsync`가 결과 필드/텍스트에 `[A-Za-z]{3,}` 영단어가 있으면 한국어로 재번역(없으면 AI 호출 안 함). 모든 장/Scene/설정 필드, 시놉시스·본문·일관성 결과에 적용됩니다. `ExtractSettingsAsync`(설정+인물, JSON 객체 → `ExtractedSettings`), `ExtractSynopsisAsync`, `ExtractChaptersAsync`, 그리고 "전체 분석"(`AnalyzeAllAsync`: 설정→시놉시스→장→각 장 `GenerateScenesAsync`).
  - **장편 대응**: `CondenseAsync`가 12000자 초과 원고를 9000자 청크(문단 경계)로 나눠 각각 요약 → 압축본을 만들고(맵 단계), 이후 추출은 압축본으로 수행. "전체 분석"은 압축을 한 번만 하고 재사용. 진행 상황은 `IProgress<string>`→`StatusMessage`.
  - **덮어쓰기 확인**: 각 역분석 명령은 관련 기존 데이터가 있으면 `ConfirmOverwrite` 콜백(`StoryPlannerWindow`의 `MessageBox` Yes/No)으로 확인 후 진행. `ApplySettings`/`ApplyChapters`가 Clear→재생성.
- **참고자료 연동**: 좌하단 "📎 참고자료 연동" — `ReferenceLibraryService`로 참고자료 폴더(`MainViewModel.ReferenceFolderPath`)의 `.md`를 로드(**하위 폴더까지** 스캔, 이름은 상대경로 `Characters/주인공` 형태). `ImportCharacterAsync`(`ExtractCharacterAsync`로 인물 JSON 추출 → `Characters` 추가), `IncludeReference`(선택 자료를 `Project.ReferenceNotes`에 append → `BuildBible`의 `[참고자료]` 섹션으로 모든 생성 프롬프트에 반영), `ExportCharacterAsync`(등장인물 → 참고자료 폴더 `Characters/이름.md`로 역내보내기).
- **참고자료 폴더 계층 관리**: 참고자료 서랍 헤더의 "폴더 만들기"(`CreateReferenceFolderCommand`)로 루트 폴더를 지정하면 **유형별 하위 폴더**(`ReferenceSubFolders`: Characters/World/Backgrounds/Synopsis/Descriptions/Illustrations)를 함께 생성하고 그 폴더를 참고자료 폴더로 설정합니다. **소설별로 다른 폴더**를 만들어 분리 관리합니다.
- **참고자료 폴더 자동 분류**: 생성기는 유형별 하위 폴더(`SubFolderFor`: 캐릭터→`Characters`, 세계관→`World`, 배경→`Backgrounds`, 묘사→`Descriptions` 등)에 저장. 생성→저장→스토리 플래너 활용/역내보내기가 한 흐름으로 이어집니다.
- **삽화 생성(이미지 · 캐릭터 연동)**: `ImageGenService`(AUTOMATIC1111 SD WebUI `/sdapi/v1/txt2img`, 기본 `127.0.0.1:7860`, **텍스트 LLM과 별도 서버**). 프롬프트는 `StoryPlannerService.GenerateCharacterImagePromptAsync`/`GenerateSceneImagePromptAsync`(LLM이 **영어 SD 프롬프트** 생성). 캐릭터 이미지 생성 시 `StoryCharacter.AppearancePrompt`(외형)를 저장하고, **씬 삽화는 그 씬에 등장하는 인물의 AppearancePrompt를 자동 삽입**해 일관성 유지. 결과는 `Illustrations/`·`Characters/Sheets/`에 PNG로 저장(`SceneNode.IllustrationPath`/`StoryCharacter.ReferenceImagePath`), 시드(`IllustrationSeed`/`ImageSeed`) 재사용. UI: 인물 카드 "이미지 생성"·Scene 편집 "삽화 생성" + `PathToImageConverter` 미리보기. 화풍은 `StoryProject.ImageStylePrefix` 공통 적용. 설계 문서 `AI_삽화_생성_설계.md` 참고.
  - **화풍(이미지 스타일) 통합 설정**: `Models/ImageStyleSettings`(프리셋 라벨 + 품질·조명·색감 라벨 + 추가/제외 프롬프트) + `ImageStyleCatalog`(한국어 라벨 → 영어 프롬프트 조각 매핑, `Presets`: 스토리북/실사/2D 애니/반실사/수채화/유화/만화/픽셀아트/3D 렌더/동양화/**지브리풍/웹툰풍/극화체**, 각 프리셋이 **추천 해상도(Width/Height)·Steps**도 보유, `FindPreset`/`BuildPositivePrefix`/`BuildNegative`). `ApplyImageStyle`이 프리셋 해상도를 `_imageService.Comfy.Width/Height`에 항상 적용하고 Steps는 **현재 Steps≥10일 때만**(FLUX/Turbo 같은 저스텝 모델 보호) 적용. UI에 해상도/스텝 미리보기(`StyleResolutionPreview`). `MainViewModel`이 `StylePreset`/`StyleQuality`/`StyleLighting`/`StyleColorMood`/`StyleExtraPositive`/`StyleExtraNegative` 속성 + `Style*Options` 목록으로 노출, `ApplyImageStyle`이 (1) `_imageService.Comfy/A1111.NegativePrompt`에 부정 프롬프트, (2) `CurrentStylePrefix`(긍정 접두) 갱신, (3) `CurrentProject.Story.ImageStylePrefix`에 반영. **스토리 플래너**는 열 때 `project.ImageStylePrefix = CurrentStylePrefix`로 주입받고, **참고자료 생성기**는 `StylePrefix`(구도 힌트 `CompositionHintFor`와 결합)로 사용. 영속: 전역 기본은 `AppSettings.ImageStyle`, 작품별은 `NovelProject.Image.Style`(둘 다 `LoadStyle`로 로드). UI는 `ImageServerWindow`의 "🎨 화풍" 구역(프리셋/품질/조명/색감 ComboBox + 추가/제외 TextBox + 긍정/부정 미리보기) **및 화풍 팝업 `ImageStyleDialog`**(`image_generation_ui_design.md` 구현). **화풍 팝업**: 생성 버튼을 누르면 `ConfirmStyleBeforeGenerate` 콜백으로 먼저 뜸(`MainWindow.ShowImageStyleDialog`). 프리셋/품질/CameraAngle/조명/색감/Mood/Background/TimeOfDay/ContentRating ComboBox + **슬라이더 3종**(Realism/Detail/BackgroundComplexity) + 추가/제외 + 미리보기. **인물 문맥 인식**: `ConfirmStyleBeforeGenerate(bool isPerson)`로 `StyleIsPersonSubject` 세팅(캐릭터 생성=true, 씬·비캐릭터=false). **인물 전용 옵션(촬영범위 Shot·인물 수 CharacterCount)은 인물 생성일 때만** 노출(`StyleIsPersonSubject`로 Visibility, 비인물이면 `ApplyImageStyle`이 프롬프트에서 Shot/CharacterCount 비움). **콘텐츠 등급(18+ 포함)은 인물·자유 생성 모두 항상 선택 가능**(자유 생성도 성인물 가능). 다중 인물은 `ImageStyleCatalog.CharacterCounts`(자동/1~3명/다수→`solo`/`two people`/`group` 등). [이미지 생성]=DialogResult true→진행(현재 화풍을 `Project.ImageStylePrefix`/`StylePrefix`에 반영), [취소]=생성 취소. `ImageStyleCatalog`에 `Shots`/`CameraAngles`/`Moods`/`Backgrounds`/`TimesOfDay` + `ContentRatingNegative`(전체/12+/15+에 NSFW 부정 추가) + `ContentRatingPositive`(**18+는 `nsfw, explicit, adult content, uncensored`를 긍정에 추가해 성인물 명시 유도** — 무검열 모델일 때 실제 반영; 등장인물 연령과 별개). 확장 필드는 모두 `ImageStyleSettings`에 있어 AppSettings/NovelProject에 통째로 영속. **삽화 구역(등장인물/Scene)에 "연결 확인"(`CheckImageServerCommand`→`IImageBackend.IsRunningAsync`)·"서버 실행"(`LaunchImageServerCommand`→`LaunchImageServerCallback`, 8초 뒤 자동 연결확인)·"서버 설정"(`OpenImageServerCommand`→`OpenImageServerSettings` 콜백으로 `ImageServerWindow` 오픈) 버튼 + `ImageServerStatus`** 제공 — 스토리 플래너에서 벗어나지 않고 서버 상태 확인·실행 가능. `LaunchImageServerCallback`/`OpenImageServerSettings`는 `MainWindow`가 주입(`MainViewModel.LaunchImageServerCommand` 재사용). 생성 실패 시 메시지가 백엔드 중립(“이미지 서버 실행/모델 확인”). 스토리 플래너는 백엔드 라우터(`IImageBackend`)를 공유하므로 A1111/ComfyUI 어느 쪽이든 그대로 동작. **기본 백엔드는 `ComfyUI`**(`AppSettings.ImageBackend` 기본값).
- **파일 저장/열기**: 하단 "파일로 저장"/"열기"는 `StoryProjectService.SaveToPathAsync`/`LoadFromPath`(임의 `.json`). `Project`는 `ObservableProperty`라 열기 시 통째로 교체되며 트리·폼이 갱신됩니다. 상단 "저장"은 기본 경로(`story_project.json`) 자동 저장.
- **주의**: 타이머(집중 타이머) 기능은 제거되었습니다. 일일 목표/진행률과 집중 모드(F11 전체화면)는 유지됩니다.

### AI 채팅 서랍 (왼쪽)

질문/대답형 AI 어시스턴트를 왼쪽 `DrawerHost.LeftDrawerContent` 서랍에 제공합니다.

- `ChatService.AskAsync`가 대화 히스토리(`ChatTurn` = system/user/assistant)를 OpenAI 호환 `/chat/completions`로 보냅니다. BaseUrl/Model은 오타 보정과 **동일한 AI 설정**(`AiBaseUrl`/`AiModel`)을 공유하고, 로컬 서버는 키 없이 호출합니다.
- `MainViewModel`: `ChatMessages`(ObservableCollection), `ChatInput`, `IsChatDrawerOpen`, `IsChatBusy`. `SendChatAsync`가 시스템 프롬프트 + 전체 히스토리를 구성해 전송하고 응답을 추가합니다(실패 시 `ChatFailed` 안내).
- View: 말풍선은 `DataTemplate.Triggers`로 `IsUser`에 따라 좌/우 정렬·색을 바꿉니다. 새 메시지가 오면 `MainWindow`가 `ChatScrollViewer.ScrollToEnd()`로 자동 스크롤. 입력창 Enter로 전송(`KeyBinding`).
- 좌우 서랍은 독립적으로 동시에 열 수 있습니다.

### 참고자료 서랍 (오른쪽 MD 뷰어)

시놉시스·캐릭터 설정 등 `.md` 참고자료를 오른쪽 `materialDesign:DrawerHost` 서랍에 표시합니다.

- **소스**: `AppSettings.ReferenceFolder`에 저장된 폴더를 `ReferenceLibraryService.LoadFolder`가 스캔(최상위 `*.md`만, 이름순). 툴바의 책 아이콘으로 서랍을 토글하고, 폴더 열기(📂)로 `Microsoft.Win32.OpenFolderDialog`(.NET 8)를 띄웁니다.
- **렌더링**: `MarkdownFlowDocumentConverter`(IValueConverter)가 MD 원문 → `FlowDocument`로 경량 변환. 지원 서식은 제목(`#`~`###`), 굵게(`**`), 목록(`-`/`*`)뿐입니다(외부 라이브러리 없음). `FlowDocumentScrollViewer.Document`에 바인딩됩니다.
- **상태**: `MainViewModel.References`(ObservableCollection), `SelectedReference`, `IsReferenceDrawerOpen`. 폴더 경로는 설정에 저장되어 다음 실행 시 자동 로드(`InitializeAsync` → `LoadReferences`).

### 에디터: RichTextBox + 서식 툴바

메인 에디터는 **RichTextBox**입니다(부분 서식 지원). `MainViewModel.Content`(string)가 여전히 단일 진실원(통계·저장·오타검사·삽입)이며, `MainWindow`가 **양방향 동기화**합니다:

- 편집 → `EditorOnTextChanged` → `RichTextBoxHelpers.GetPlainText` → `Content`.
- `Content` 외부 변경(새 문서/열기/오타 교체/스토리 삽입) → `OnViewModelPropertyChanged` → `SyncEditorFromViewModel` → `SetPlainText`. 순환은 `_syncingEditor` 플래그로 방지.
- **본문 이미지 삽입(영구 보존)**: 생성 이미지를 원고에 삽입하면 평문 Content에 토큰 `⟦IMG:경로⟧`으로 저장되어 **프로젝트(.novel)·txt에 그대로 보존**되고, `RichTextBoxHelpers.SetPlainText`가 토큰을 실제 `InlineUIContainer(Image)`로 렌더링(경로는 `Image.Tag`). `GetPlainText`는 컨테이너를 다시 토큰으로 복원, `GetOffset`/`GetPointerAtOffset`은 **컨테이너를 토큰 길이만큼의 문자로 계산**해 맞춤법 오프셋 일관성 유지. 삽입은 `MainWindow.InsertImageIntoEditor`(커서 오프셋에 토큰 삽입 → Content 변경 → 재렌더). 진입: 스토리 플래너 씬 "본문에 삽입"(`InsertSceneImageCommand`)·캐릭터 카드 "본문에 삽입"(`InsertCharacterImageCommand`), 참고자료 생성기 "본문에 삽입"(`InsertImageCommand`) — 모두 `InsertImageToEditor` 콜백. **주의**: 파일 없으면 토큰 텍스트로 대체(경로 유지). **상대경로 저장(이식성)**: `RichTextBoxHelpers.ImageBaseFolder`(현재 작품 폴더, `SyncEditorFromViewModel`에서 설정) 기준으로, 삽입 시 폴더 안 이미지는 `ToPortablePath`로 상대경로(`Illustrations/xxx.png`)로 저장하고 렌더 시 `ResolveImagePath`로 절대경로 복원 → 폴더째 이동해도 이미지 유지. **DOCX 내보내기 이미지 포함**: `DocxExportService.AppendImage`가 `InlineUIContainer(Image)`를 `AddImagePart`+`BuildInlineDrawing`(EMU 변환, 최대 6인치)으로 임베드(png/jpg/gif/bmp).
- **문자 오프셋 통일**: 문단/줄바꿈을 모두 `\n` 1글자로 취급(`RichTextBoxHelpers`의 GetPlainText/GetPointerAtOffset/GetOffset). `StatisticsService`도 `\n` 기준. 오타 `TypoMark`의 offset과 어도너·우클릭·`VisibleRangeResolver`(`GetPositionFromPoint`→`GetOffset`)가 이 규칙을 공유합니다.
- **서식 툴바**(두 번째 툴바 행, 코드비하인드 이벤트): 글꼴/크기 ComboBox, 굵게·기울임·밑줄 토글(`Selection.ApplyPropertyValue`), 글자색·하이라이트(`ShowColorMenu` 팔레트 ContextMenu → Foreground/Background), 서식 지우기(`ClearAllProperties`).
- **DOCX 서식 저장**: `.docx`로 저장/내보내기 시 `DocxDocumentSaver` 콜백(View)이 `DocxExportService.ExportFlowDocumentAsync`로 **RichTextBox FlowDocument → DOCX 서식**(굵게·기울임·밑줄·글자색·크기·하이라이트)을 매핑해 저장합니다. **편집기 기본 24px = 11pt** 기준: `sz`(하프포인트) = `px * 11/12`. txt/md는 평문 저장.
- **편집기 폰트 크기**: `EditorFontSize`(기본 24px)를 설정 창 슬라이더로 조절, RichTextBox `FontSize`에 바인딩. 화면은 크게 보이되 DOCX 저장은 11pt로 나갑니다.
- **Ctrl+휠 확대/축소**: `MainWindow.EditorOnPreviewMouseWheel`이 Ctrl+휠 시 `EditorScale`(RichTextBox `LayoutTransform`의 `ScaleTransform`)을 0.5~3.0으로 조절 — **폰트 크기(EditorFontSize)와 무관한 시각 확대**(저장/DOCX에 영향 없음). LayoutTransform이라 텍스트가 확대 폭에 맞춰 재배치됨(가로 오버플로 없음).
- **본문 이미지 가운데 정렬**: `SetPlainText`가 **줄 전체가 이미지 토큰 하나뿐인 줄**(`PureImageLineRegex`)은 `TextAlignment=Center`인 별도 문단으로 렌더링(텍스트 줄은 성능 위해 단일 문단+LineBreak 유지). 문단 경계는 `\n` 1글자로 세므로 오프셋 일관성 유지(삽입은 `\n토큰\n`이라 대부분 단독 줄).
- **참고자료 생성기**(메뉴 "참고자료 생성기" → `ReferenceGeneratorWindow`/`ReferenceGeneratorViewModel`): 유형(캐릭터/세계관/시놉시스 등) + 요청으로 `ChatService`가 **마크다운(.md)** 생성 → 편집 후 `.md` 저장. **제목·요청 자동 생성**(`AutoFillCommand`, "🎲" 버튼): 선택 유형에 맞는 소재를 무작위로 지어 `Title`/`Prompt`를 채움(`ThemeSeeds`+랜덤 시드로 다양성 유도, AI가 `{"title","request"}` JSON 반환 → `TryParseTitleRequest`로 파싱, 캐릭터 계열은 인물 이름을 title로). 저장 후 참고자료 서랍을 새로고침합니다. 유형에 "묘사·표현 모음/감정·심리 묘사/배경·풍경 묘사/대사·문장 모음"이 있고, 이 계열은 `BuildSystemPrompt`가 **소설 문장 특화 프롬프트**(참신한 묘사·표현을 바로 쓸 수 있는 문장으로)로 전환합니다. **이미지 생성**도 지원(생성자에 `IImageBackend`+참고자료 폴더 주입): "🖼 이미지 생성" 구역에서 연결 확인/서버 실행/서버 설정 + `GenerateImageCommand` — 프롬프트가 비면 `BuildImagePromptAsync`(ChatService가 생성 내용→영어 SD 프롬프트)로 자동 생성, 유형별 화풍 접두(`ImageStylePrefixFor`)·저장 하위폴더(`ImageSubFolderFor`: 캐릭터→`Characters/Sheets`, 배경→`Backgrounds`, 그 외→`Illustrations`) 적용, `BuildFileNameBase` 이름으로 PNG 저장 후 `PathToImageConverter` 미리보기. **설정 시트 스타일(캐릭터 한정)**: `CompositionHintFor`가 **캐릭터만** 모델 시트/턴어라운드(정면·후면·측면 전신 + 다각도 얼굴 + 표정 시트 + T-pose)로 구도 강제, 배경은 일반 배경 아트(`scenery, background art`), 그 외는 강제 구도 없음(내용에 맞는 일러스트). `SheetResolutionFor`도 **캐릭터만** 가로 캔버스(1216×832)로 임시 전환 후 원복(`_imageBackend.Width/Height` save/restore), 나머지는 화풍 해상도 그대로. **재생성 미리보기 갱신**: 같은 파일명으로 덮어쓰면 경로가 동일해 바인딩이 안 바뀌므로, 저장 후 `GeneratedImagePath`(스토리 플래너는 `ReferenceImagePath`/`IllustrationPath`)를 **빈 값→경로로 다시 설정**해 강제 리바인딩(`PathToImageConverter`가 `IgnoreImageCache`라 새 파일 로드). **이미지 파일로 저장**: `SaveImageAsFileCommand`+`ImageSaveAsResolver`(SaveFileDialog, `MainWindow` 주입) → `File.Copy(overwrite)`. 서버 실행/설정 콜백은 `MainWindow`가 주입.
- **파일명 규칙**: `BuildFileNameBase`가 베이스명을 만들고(캐릭터는 생성 결과에서 이름·나이·직업 추출 → `이름_나이_직업`, 그 외는 제목), View가 유형 폴더의 기존 `.md` 개수로 **넘버링 접두**(`0000_`)를 붙입니다 → 예: `0000_홍길동_35세_형사.md`.

### 이미지 생성(삽화) + 서버 설치 도우미

`AI_삽화_생성_설계.md`를 구현. 캐릭터·장면 삽화를 로컬 이미지 서버로 생성하며 **Ollama(텍스트)와 완전히 별개**입니다. **백엔드는 두 종류**가 선택 가능합니다(설정에서 전환):

- **백엔드 추상화**: `IImageBackend`(`BaseUrl`/`Width`/`Height`/`Steps`/`CfgScale`/`NegativePrompt` + `IsRunningAsync`/`GenerateAsync(prompt, seed)→ImageGenResult`). `ImageGenService`(A1111)와 `ComfyUiImageService`가 각각 구현. `ImageServiceRouter`(역시 `IImageBackend`)가 `A1111`/`Comfy` 인스턴스를 들고 `Backend`(`ImageBackendKind`)에 따라 위임 — **스토리 플래너/메인 뷰모델은 이 라우터 하나만 공유**하고 백엔드는 런타임 전환. `AppSettings.ImageBackend`("A1111"/"ComfyUI")로 영속.
- **A1111**(`ImageGenService`): `record ImageGenResult(byte[] ImageBytes, long Seed)`. `IsRunningAsync`(GET `/sdapi/v1/sd-models`), `GenerateAsync(prompt, seed)`(POST `/sdapi/v1/txt2img`, base64→bytes, info JSON에서 seed 회수). Sampler 등 프로퍼티. BaseUrl은 `AppSettings.ImageBaseUrl`(기본 `http://127.0.0.1:7860`).
- **ComfyUI**(`ComfyUiImageService`, 최신 모델·FLUX용): A1111과 API가 완전히 다름 — **워크플로우(노드 그래프) JSON**을 POST `/prompt`(+client_id)로 큐잉→`prompt_id`→`/history/{id}` 폴링(최대 5분, 2초 간격)→`/view?filename=..&subfolder=..&type=..`로 PNG 회수. `IsRunningAsync`는 GET `/system_stats`. 체크포인트는 `CheckpointName` 지정, 비었으면 `/object_info/CheckpointLoaderSimple`에서 첫 번째 자동 선택. `BuildWorkflow`가 기본 txt2img 그래프(CheckpointLoaderSimple→EmptyLatentImage/CLIPTextEncode×2→KSampler→VAEDecode→SaveImage) 생성. seed 음수면 `Random.Shared`로 실수. 기본 해상도 832×1216(SDXL 세로), Sampler `dpmpp_2m`/`karras`. BaseUrl은 `AppSettings.ComfyUiBaseUrl`(기본 `http://127.0.0.1:8188`).
- **프롬프트**(`StoryPlannerService`): `GenerateCharacterImagePromptAsync`/`GenerateSceneImagePromptAsync`가 영어 SD 프롬프트 생성. Scene 프롬프트는 **등장인물의 `AppearancePrompt`를 자동 포함**해 캐릭터 일관성 유지. 스타일 접두는 `StoryProject.ImageStylePrefix`.
- **데이터**(`StoryModels.cs`): `StoryCharacter`에 `AppearancePrompt`/`ReferenceImagePath`/`ImageSeed`, `SceneNode`에 `IllustrationPath`/`IllustrationPrompt`/`IllustrationSeed`. 이미지는 참고자료 폴더 하위(`Characters/`, Scene은 별도 subdir)에 png 저장(`StoryPlannerViewModel.SaveImage`). `PathToImageConverter`(OnLoad+IgnoreImageCache)로 UI 미리보기.
- **UI**: 스토리 플래너 캐릭터 카드/Scene 편집 삽화 구역에 "이미지 생성" 버튼 + 미리보기.
- **설치 도우미(초보자용, Python·git까지 자동)**: `ImageSetupService` — **AUTOMATIC1111은 Python 3.10/3.11 필요**(최신 3.12+/3.14는 `torch==2.1.2` 휠이 없어 실패). `InstallAsync(dir, progress)`가 (1) `EnsureGitAsync`(없으면 `winget install Git.Git`), (2) `EnsurePythonAsync`(`FindCompatiblePython`으로 3.10/3.11 탐색 → 없으면 `winget install Python.Python.3.10 --scope machine`), (3) `git clone --depth 1` AUTOMATIC1111, (4) `SetPythonInBat`(webui-user.bat의 `set PYTHON=`에 3.10 경로 지정), (5) `RemoveIncompatibleVenv`(`venv/pyvenv.cfg`가 3.10/3.11이 아니면 삭제해 재생성 유도), (6) `EnsurePipConstraints`(**최신 setuptools 81+가 `pkg_resources`를 제거해 CLIP/gfpgan 등 옛 패키지 빌드가 깨지는 문제 방어** — `pip-constraints.txt`에 `setuptools<70`을 쓰고 webui-user.bat에 `set PIP_CONSTRAINT=`를 지정해 pip 빌드 격리 환경에도 옛 setuptools 강제), (7) `EnsureApiFlag`(`set COMMANDLINE_ARGS=`에 `--api`) 순으로 진행. `LaunchImageServer`(A1111)도 실행 직전 `EnsureApiFlag`/`EnsurePipConstraints`/`EnsureExtraArgs`를 호출해 기존 설치도 자동 패치. `Launch(dir)`(webui-user.bat 실행, UseShellExecute). `IsWingetAvailable`/`IsGitAvailable`/`IsInstalled`도 제공. 진행 로그는 `IProgress<string>`. **주의**: winget 설치는 UAC(관리자) 창을 띄우고, 설치 직후 PATH 갱신 전이라 `FindCompatiblePython`은 알려진 설치 경로(`C:\Program Files\Python310` 등)와 `py -3.10` 런처로 재탐색한다.
- **ComfyUI 설치 도우미(포터블, Python 불필요)**: `ComfyUiSetupService` — A1111의 Python 지옥을 피하려 **embedded Python 포함 포터블 .7z**(`.../releases/latest/download/ComfyUI_windows_portable_nvidia.7z`)를 사용. `InstallAsync`가 (1) `EnsureSevenZipAsync`(7z.exe 탐색, 없으면 `winget install 7zip.7zip`), (2) 스트리밍 다운로드(`DownloadAsync`, ~1.5~2GB, %진행률 보고), (3) `7z x`로 압축 해제 순. `ResolveRunDirectory`(포터블은 `ComfyUI_windows_portable/` 하위에 `run_nvidia_gpu.bat`), `Launch`(GPU→CPU bat), `GetCheckpointsFolder`(`ComfyUI/models/checkpoints`). **모델은 미포함**이라 "모델 폴더 열기" 버튼도 제공하지만, **추천 모델 자동 다운로드**를 지원: `record ComfyModel`(파일명·직접 URL·권장 Steps/Cfg/Sampler/Scheduler/Width/Height·안내)의 정적 목록 `RecommendedModels`(8GB 기준 — **SDXL Base 1.0**/OpenRAIL++, **SDXL Turbo**/비상업·초고속, **FLUX.1 schnell fp8**/Apache-2.0·상업가능·~17GB, 모두 HuggingFace 공개 resolve URL·인증 불필요). `DownloadModelAsync`가 체크포인트 폴더로 스트리밍 다운로드 — **임시 `.part`로 받고 완료·크기검증(Content-Length 일치) 후 최종 이름으로 원자적 이동**(불완전 파일이 모델로 잡혀 ComfyUI `ModelMMAP`이 `OS Error 32`로 죽는 문제 방지; 최종 파일명 존재=완료로 간주해 skip). 다운로드 성공 시 `MainViewModel.ApplyComfyModel`이 `Comfy.CheckpointName` + 그 모델의 권장 샘플링 설정을 백엔드에 적용(FLUX는 cfg=1·4스텝·euler/simple처럼 모델별로 다름). 선택 모델 파일명은 `AppSettings.ComfyUiCheckpoint`로 영속되고, 재시작 시 `RecommendedModels`와 파일명이 일치하면 권장 설정도 복원.
- **모델 없으면 자동 다운로드·연결**: `ComfyUiImageService.ListCheckpointsAsync`/`HasCheckpointAsync`(서버 `/object_info`로 설치된 체크포인트 조회, `CheckpointName` 유효성 보정). `MainViewModel.EnsureComfyModelReadyAsync`(공개)가 (1) 서버 실행 확인 → (2) 모델 있으면 그대로 사용 → (3) 없으면 `ConfirmImageModelDownload` 콜백(Yes/No)로 확인 후 `SelectedComfyModel` 자동 다운로드+`ApplyComfyModel`. `EnsureComfyModelCommand`(버튼) + 스토리 플래너/참고자료 생성기의 **삽화 생성 직전 `EnsureImageModel` 콜백으로 자동 호출**(모델 미준비면 생성 취소+안내). UI: **서버 제어 버튼(연결확인/서버실행/서버설정/모델준비)은 이미지 서버 설정 창(`ImageServerWindow`)에만** 둠. 스토리 플래너·참고자료 생성기의 삽화 구역에는 버튼 없이 `ImageServerStatus` 텍스트만 표시(모델 준비는 생성 직전 `EnsureImageModel` 콜백으로 자동 수행). 이미지 서버 창에 "모델 자동 준비"(`EnsureComfyModelCommand`). **주의**: 다운로드 직후 ComfyUI가 파일을 못 잡으면 재시작 안내(`HasCheckpointAsync` false 시).
- **무검열(NSFW 가능) 모델**: `ComfyModel.Uncensored=true` 계열 — **RealVisXL V4.0**(실사), **Illustrious-XL v0.1**(애니), **NoobAI-XL v1.0**(애니), 모두 HuggingFace 공개 URL. 로컬 생성이라 세이프티 체커가 없음(모델이 콘텐츠를 결정). UI는 🔞 접두 + 선택 시 주황색 경고(법규·라이선스·성인 개인 용도 안내)를 표시. **URL은 추가 전 HEAD 200 확인 필수**(커뮤니티 모델은 파일명 변경으로 404 위험).
- **하드웨어(VRAM) 프로파일**(`Models/HardwareProfile.cs`, `MainViewModel.HardwareProfiles`): Auto/High(12GB+)/Medium(8GB)/Low(6GB↓)/Cpu. 각 프로파일이 `ComfyArgs`(예 `--lowvram`/`--cpu`/`--highvram`)와 `A1111Args`(예 `--medvram`/`--lowvram`)를 가짐. 실행 시 `ComfyUiSetupService.Launch(dir, comfyArgs)`(embedded python에 인자 직접 전달, `--cpu`면 run_cpu.bat 폴백)와 `ImageSetupService.EnsureExtraArgs(dir, a1111Args)`(webui-user.bat COMMANDLINE_ARGS에 없는 토큰만 추가, `--api` 유지)로 반영. 선택은 `AppSettings.ImageHardware`로 영속. UI는 상단 백엔드 카드에 ComboBox+안내.
  - **진입/UI**: 상단 메뉴 "이미지 서버 설정" → `ImageServerWindow`(DataContext=`MainViewModel` 공유). **백엔드는 ComfyUI로 고정**(A1111은 UI에서 제거 — `ApplySettings`가 `UseComfyUi=true` 강제). 창은 사양(VRAM) + [연결 확인]/[서버 실행], ComfyUI 그룹(주소·자동설치·[ComfyUI 실행]·모델 폴더·모델 다운로드)만 표시. A1111 서비스 클래스(`ImageGenService`/`ImageSetupService`)와 라우터 A1111 분기는 코드에 남아 있으나 UI로는 접근 불가(백엔드 전환 라디오·A1111 그룹 삭제). `MainViewModel`: `UseComfyUi`/`ImageBaseUrl`/`ImageWebUiPath`/`ComfyUiBaseUrl`/`ComfyUiPath`/`ImageServerStatus`/`ImageSetupLog`/`IsImageSetupBusy` + `TestImageServer`(양 URL 세팅 후 active 테스트)/`InstallImageServer`(A1111)/`InstallComfyUi`/`LaunchImageServer`(active)/`OpenComfyModelsFolder`/`DownloadComfyModel`(추천 모델 다운로드+설정 적용)/`OpenImageServerPage` 명령. ComfyUI 그룹에 모델 선택 ComboBox(`ComfyModels`/`SelectedComfyModel`)+안내(`.Note`)+다운로드 버튼. `OnImageBaseUrlChanged`→`A1111.BaseUrl`, `OnComfyUiBaseUrlChanged`→`Comfy.BaseUrl`, `OnUseComfyUiChanged`→`Router.Backend`. "저장"은 `SaveSettingsCommand`.
  - **주의**: `MainViewModel` 생성자에 `ImageServiceRouter`/`ImageSetupService`/`ComfyUiSetupService`가 추가됨(DI 없음 → `MainWindow` 생성자에서 수동 조립). 라우터는 스토리 플래너와 **같은 인스턴스** 공유(스토리 플래너 파라미터 타입은 `IImageBackend`). `InverseBoolConverter`(`x:Key=InverseBool`)는 대상이 `Visibility`면 반전값을 Visible/Collapsed로 반환. 두 서버 모두 첫 실행 시 수 GB를 추가 다운로드하므로 자동 시작하지 않고 버튼으로만 실행.

### 데이터/설정 경로

`%LocalAppData%\NovelWriter\` 아래에 저장됩니다: `novel_writer.db`(SQLite 문서), `settings.json`(`SettingsService`, `AppSettings` JSON), `Backups\`(`BackupService`). `AppSettings`에는 `ImageBaseUrl`/`ImageWebUiPath`(이미지 서버)도 포함됩니다.

### AI 오타 보정 (`TypoCorrectionService`)

OpenAI 호환 Chat Completions API를 호출합니다. 서버 주소·모델은 **환경변수 > 앱 설정(`AppSettings.AiBaseUrl`/`AiModel`) > 기본값** 순으로 결정됩니다.

- 환경변수 override: `NOVEL_WRITER_OPENAI_API_KEY`, `NOVEL_WRITER_OPENAI_BASE_URL`, `NOVEL_WRITER_OPENAI_MODEL`.
- 앱 설정 기본값은 **로컬 EXAONE(Ollama)** 를 향합니다: `AiBaseUrl=http://localhost:11434/v1`, `AiModel=exaone3.5:7.8b`.
- **로컬 서버 특례**: `BaseUrl`이 `localhost`/`127.0.0.1`/`0.0.0.0`이면 API 키 없이도 AI 경로를 호출합니다. 원격 주소는 기존처럼 키가 있을 때만 호출합니다.
- API 오류는 모두 삼켜지고 하드코딩된 오타 딕셔너리 fallback으로 처리됩니다(예외를 밖으로 던지지 않음).
- **모델 선택은 설정 창의 편집형 ComboBox**(`AvailableModels`)에서 합니다. 목록 = 추천 모델(EXAONE/Qwen2.5/Gemma/Llama/Mistral/Phi/DeepSeek) + Magnum(창작 특화) + 무검열 모델(`uncensored_llm_guide.md`: dolphin3, mannix/llama3.1-8b-abliterated, nous-hermes3) + `OllamaService.ListInstalledModelsAsync`로 가져온 **실제 설치된 모델**(중복 제거). 임의 모델명 직접 입력도 가능. 선택은 `AppSettings.AiModel`에 저장되고 오타 보정·채팅·스토리 플래너가 공유합니다.
- **모델 배지**(ComboBox 드롭다운 항목): **설치됨**(초록, `InstalledModelBadgeConverter` — `MainViewModel.InstalledModels`와 모델명을 MultiBinding으로 대조), **한글 특화**(파랑, `KoreanModelBadgeConverter` — exaone/kanana/korean 등), **노벨 특화**(보라, `NovelModelBadgeConverter` — magnum/novel/story/writer), **무검열 노벨 특화**(주황, `UncensoredModelBadgeConverter` — uncensored/abliterated/dolphin/hermes). 키워드는 `ModelBadgeKeywords`. `InstalledModels`는 `LoadInstalledModelsAsync`에서 `ObservableProperty`로 세팅되어 배지가 갱신됩니다.

### 맞춤법 검사(붉은 물결선)와 교정 — Hunspell 우선, AI는 보조

맞춤법 감지의 **1차는 Hunspell 한국어 사전**(오프라인·실시간)이고, AI는 문맥/문법 등 "필요할 때만" 보조하는 역할입니다. 파이프라인: **① 사용자 사전 → ② Hunspell → 빨간 밑줄 + 추천 → ③(향후) AI 문맥**.

- **사전**: `Dictionaries/ko.aff`(11MB)·`ko.dic`(2.8MB) — spellcheck-ko/hunspell-dict-ko 0.7.94, **GPL-3.0**. csproj `Content`로 출력에 복사. `HunspellSpellCheckService.LoadAsync`가 `AppContext.BaseDirectory/Dictionaries`에서 백그라운드 로드(~0.8초). 파일 없으면 검사를 건너뜁니다(graceful).
- **검사 흐름**(`MainViewModel.RunSpellCheck`): 뷰포트 범위(`VisibleRangeResolver`)에서 `[가-힣]+` 어절을 뽑아, `UserDictionaryService.Contains`(사용자 사전)와 세션 `_ignoredWords`(무시)를 먼저 거르고, `Hunspell.Check`가 false인 어절만 `TypoMark`로 만듭니다. 추천(`Suggest`)은 비용이 크므로 **우클릭 시점에 lazy 계산**(`GetSuggestions` 캐시).
- **트리거**: 편집(`OnContentChanged`)·스크롤(View의 ScrollChanged)에서 `RequestSpellCheck`로 700ms 디바운스 후 재검사. 버튼(툴바 AutoFix)은 `FixTyposCommand`로 즉시 재검사.
- **렌더링**: `SpellingAdorner`가 `RichTextBoxHelpers.GetPointerAtOffset` + `TextPointer.GetCharacterRect`로 좌표를 얻어 빨간/파란 물결선을 그립니다. OS 맞춤법(`SpellCheck.IsEnabled=False`)은 끄고 이 표시만 사용합니다.
- **자모 추천 재정렬**: `HunspellSpellCheckService.Suggest`는 Hunspell 후보를 `HangulJamo.Distance`(음절을 초/중/종성으로 분해한 뒤의 편집거리)로 재정렬합니다. `안뇽→안녕`처럼 자모가 가까운 후보가 상단에 옵니다(SymSpell식 자모 근접, 별도 사전 데이터 불필요).
- **우클릭 메뉴**(`MainWindow.EditorOnContextMenuOpening`): 추천 단어들 + `무시`(`IgnoreWord`) + `사전에 추가`(`AddWordToDictionary`). 추천 선택 시 `ApplyReplacementPreservingScroll` → `ApplyReplacement(mark, 선택어)`가 해당 구간만 교체(뒤쪽 마크 위치는 delta 보정).
  - **스크롤 유지**: `Content`(전체 문자열) 교체 시 TextBox가 맨 위로 튀므로, 교체 전 `VerticalOffset`/`HorizontalOffset`을 저장하고 `Dispatcher.BeginInvoke(Loaded)`로 복원합니다.
- **AI 문맥 검사(③, 파란 밑줄)**: 보기 메뉴 "AI 문맥 검사"(`CheckContextCommand`)가 현재 보이는 범위를 `TypoCorrectionService.DetectAsync`(AI)로 검사해 `TypoKind.Context` 마크를 추가합니다. `SpellingAdorner`가 종류별로 색을 구분(맞춤법=빨강, 문맥=파랑). `RunSpellCheck`는 `TypoKind.Spelling` 마크만 교체하므로 파란 문맥 마크는 유지됩니다(`RemoveMarksOfKind`).
- **전역 예외 핸들러**: `App.DispatcherUnhandledException`이 UI 스레드 예외를 잡아 `%LocalAppData%/NovelWriter/error.log`에 기록하고 앱을 유지합니다.

### 저장/설정

- **다른 이름으로 저장**(파일 메뉴): `SaveAsCommand` → `SaveAsPathResolver`(SaveFileDialog, txt/md/docx). 확장자가 `.docx`면 `DocxExportService`, 그 외는 `File.WriteAllTextAsync`.
- **통합 설정 창(`SettingsHubWindow`)**: 기존 `SettingsWindow`/`ThemeCustomWindow`/`ImageServerWindow` 3개를 **탭 하나로 합침**(모두 삭제됨). DataContext=`MainViewModel` 공유, 하단 공통 [저장]/[닫기]([저장]=`SaveSettingsCommand`). 탭: **일반**(AI 모델 ComboBox+배지·자동저장 on/off·주기), **테마·외형**(메뉴/편집기/툴바아이콘/참고자료/채팅 크기·편집기 폰트 종류·참고자료/채팅/커스텀 배경·글자색 팔레트·배경 이미지), **이미지**(ComfyUI 서버·화풍·모델 다운로드·하드웨어·로그). 진입: 상단 "설정"→`OnOpenSettings`(일반 탭), 테마 메뉴 "커스텀 설정"→`OnOpenThemeCustom`(테마 탭), "이미지 서버 설정"→`OnOpenImageServer`(이미지 탭) 모두 `new SettingsHubWindow(_viewModel, 초기탭)`. 스토리 플래너/참고자료 생성기의 `OpenImageServerSettings` 콜백도 이미지 탭으로 오픈. 생성자에서 `RefreshInstalledModelsAsync`(배지용). 탭 상수 `TabGeneral/TabTheme/TabImage`.
- **색 팔레트**: 색 설정은 hex 입력 + `PaletteColors` 스와치(ItemsControl)에서 선택. 스와치 클릭 → `SetReferenceColorCommand`/`SetChatBackgroundCommand`/`SetCustomBackgroundCommand`/`SetCustomForegroundCommand`(CommandParameter=hex). 스와치 배경은 `StringToBrushConverter`로 hex→Brush.
- **테마 커스텀**: 설정 창 하단에서 커스텀 배경/글자색을 hex+팔레트로 지정. `SetCustomBackground`/`SetCustomForeground`가 `Theme="Custom"` + `ApplyTheme()`로 즉시 반영(`CustomBackgroundHex`/`CustomForegroundHex`).
- **에디터 폰트 종류**(`EditorFontFamilyName`→`EditorFontFamily`, RichTextBox `FontFamily` 바인딩)와 **배경 이미지**(`BackgroundImagePath`/`BackgroundOpacity`)도 커스텀 가능. 배경 이미지가 있으면 에디터 뒤 `Image` 레이어를 깔고 RichTextBox 배경을 `EditorEffectiveBackground`(투명)로 바꿔 이미지가 비칩니다. 이미지 선택은 `BackgroundImageResolver`(OpenFileDialog) 콜백.
- 참고자료 폰트/색 상속을 위해 `MarkdownFlowDocumentConverter`는 FlowDocument에 FontSize/Foreground를 지정하지 않습니다(뷰어 바인딩 값 상속).

### 로컬 모델 온보딩 (`OllamaService` + AI 준비 오버레이)

**시작 시에는 Ollama에 접촉하지 않습니다**(로딩 지연 방지). `InitializeAsync`는 Hunspell 사전만 로드하고, AI 확인/모델 목록은 **설정 창을 열 때**(`RefreshInstalledModelsAsync`)·**모델 저장 시**(`SaveSettings` → `EnsureAiReadyAsync`) 등 필요 시점에만 수행합니다(`CheckAiReadyAsync`/`RefreshInstalledModelsAsync` public).

`EnsureAiReadyAsync`가 호출되면 Ollama 상태를 확인합니다:

- Ollama 미실행 → 오버레이에서 설치 페이지 안내 + "다시 확인".
- 선택 모델 미설치 → 사용자 동의 후 `OllamaService.PullModelAsync`로 다운로드(스트리밍 진행률). 용량이 커서 자동 시작하지 않고 버튼으로만 시작합니다.
- 준비 완료 → 오버레이(`IsAiSetupVisible`)가 사라집니다.
- `OllamaService`는 **네이티브 API**(`/api/tags`, `/api/pull`)를 사용하므로, `.../v1` 주소를 `ToOllamaNativeUrl`로 변환해 주입합니다.

> EXAONE 3.5는 **비상업(NC) 라이선스**입니다. 상업 배포에는 사용할 수 없으며, 모델을 앱에 번들하지 말고 사용자 PC에서 공식 소스로 받게 해야 합니다.

## 코드 스타일 (README 프롬프트 규약)

- CommunityToolkit.Mvvm: `[ObservableProperty]`(private 필드에 부착) + `[RelayCommand]` 사용. `partial` 클래스 필수.
- `[ObservableProperty]` 부수효과는 `partial void OnXxxChanged(...)` 훅에 작성.
- 비동기는 `async/await`, 라이브러리 코드는 `ConfigureAwait(false)`.
- 들여쓰기 4칸(탭 금지). 공개 멤버에 한글 XML 문서 주석(`<summary>`) 작성.

## 주의 사항

- `bin/`, `obj/`가 git에 커밋되어 있습니다(추적됨). 소스 변경만 커밋하도록 주의하세요.
- `.github/copilot-instructions.md`는 Azure MCP 관련 규칙일 뿐 이 앱과 무관합니다.
