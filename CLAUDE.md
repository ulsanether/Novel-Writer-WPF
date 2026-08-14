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

- 테스트 프로젝트는 아직 없습니다. 테스트 추가 시 별도 xUnit/NUnit 프로젝트를 만들고 `.slnx`에 등록해야 합니다.
- Windows가 아닌 환경에서 빌드만 하려면 `EnableWindowsTargeting`이 이미 켜져 있어 `dotnet build`는 통과하지만 실행은 불가합니다.

## 아키텍처

전형적인 MVVM이되 몇 가지 비관용적(non-obvious) 결정이 있으므로 주의:

- **DI 컨테이너 없음.** 모든 서비스와 `MainViewModel`은 `MainWindow.xaml.cs` 생성자에서 **수동으로 new & 조립**됩니다(`App.xaml.cs`는 비어 있음). 새 서비스를 추가하려면 이 생성자와 `MainViewModel`의 생성자 파라미터를 함께 수정해야 합니다.
- **View → ViewModel 콜백 주입.** 파일 열기/저장 다이얼로그는 View의 관심사이므로, `MainViewModel.ImportPathResolver` / `ExportPathResolver` (`Func<Task<string?>>`)를 `MainWindow`에서 설정해 주입합니다. ViewModel은 `Microsoft.Win32` 다이얼로그를 직접 참조하지 않습니다.
- **단일 문서 저장소.** `DocumentRepository`(SQLite)는 다중 문서를 저장하지 않습니다. `SaveAsync`가 매번 `DELETE FROM Documents` 후 INSERT 하므로 **항상 문서 1개만** 유지됩니다. 다중 문서 지원은 미구현.
- **로컬라이제이션은 resx가 아님.** `LocalizationService`가 ko/en 딕셔너리를 들고 있고, `MainViewModel`이 `XxxText` 형태의 계산 속성으로 노출합니다. 언어 전환 시 `RaiseLocalizedPropertiesChanged()`가 **모든 로컬라이즈 속성에 대해 수동으로** `OnPropertyChanged`를 호출합니다 — 새 UI 텍스트를 추가하면 (1) 두 딕셔너리, (2) ViewModel 계산 속성, (3) `RaiseLocalizedPropertiesChanged()` 목록 세 곳을 모두 갱신해야 합니다.
- **집중 모드.** `IsFocusMode` 변경을 `MainWindow`가 `PropertyChanged`로 감지해 전체화면/무테두리/Topmost로 전환합니다. 상단 크롬(`ChromePanel`)은 `Window_MouseMove` + `DispatcherTimer`(3초)로 표시/자동 숨김됩니다.

### 스토리 플래너 (계층형 스토리 설계)

`AI_시놉시스_전체_설계.md`를 구현한 별도 창(`StoryPlannerWindow`, 메뉴/툴바에서 진입). **Story Bible → 시놉시스 → 장 → Scene → 본문**을 각각 **별도 AI 호출**로 생성합니다(한 프롬프트로 전부 생성하지 않음).

- **데이터**: `Models/StoryModels.cs`(`StoryProject`/`ChapterNode`/`SceneNode`/`StoryCharacter`, 모두 `ObservableObject`). `StoryProjectService`가 `%LocalAppData%/NovelWriter/story_project.json`에 저장 — **DOCX 원고와 완전히 분리**됩니다.
- **AI**(`StoryPlannerService`, `ChatService` 재사용): `GenerateSynopsisAsync`(본문 X, 큰 사건만), `GenerateChaptersAsync`/`GenerateScenesAsync`(JSON 배열 파싱), `GenerateSceneContentAsync`(본문 1패스), `GenerateSceneContentDetailedAsync`("상세 작성" — `GenerateSceneBeatsAsync`로 Scene을 4~6개 비트로 나눈 뒤 각 비트를 여러 문단으로 작성·이어붙여 **긴 본문** 생성, 진행률 보고), `CheckConsistencyAsync`(일관성 경고). 본문 생성은 **대사 비율**(`DialogueRatio` 0~100, 슬라이더)을 받아 `DialogueStyleInstruction`으로 문체 지시(0=묘사만, 100=대사만)를 프롬프트에 넣습니다. **컨텍스트 최소화**: 각 호출에 Story Bible + 시놉시스 요약 + 현재 장/이전 Scene 요약만 전달(로컬 7B 모델용). 로컬 모델의 불완전 JSON은 `[`~`]` 추출 + try/catch로 방어.
- **UI**: 좌(작품 설정+시놉시스) / 중(장→Scene `TreeView`, `HierarchicalDataTemplate`) / 우(선택 상세 편집). "본문 작성" 결과는 "에디터에 삽입"(`InsertToEditor` 콜백)으로 메인 `Content`에 append. 이 창은 한국어 UI 하드코딩(다국어화는 이후).
- **작품 설정 입력**: 장르·시대·세계관·결말은 **편집형 ComboBox**(선택 또는 직접 입력)이며 `StoryPlannerViewModel`의 `GenreSamples`/`EraSamples`/`WorldSamples`/`EndingSamples`(각 10개 이상)를 제공합니다.
- **원고 역분석(원고 → 설계)**: 창 **하단에 별도 구역**("📖 원고에서 역분석")으로 분리 — `GetManuscript` 콜백(메인 `Content`)을 AI로 분석해 설계를 채웁니다. 출력이 영어로 새는 것을 막기 위해 (1) 모든 생성/추출 프롬프트의 system에 "JSON 값은 반드시 한국어로" 지시, (2) **후처리 재번역**: `EnsureKoreanAsync`가 결과 필드/텍스트에 `[A-Za-z]{3,}` 영단어가 있으면 한국어로 재번역(없으면 AI 호출 안 함). 모든 장/Scene/설정 필드, 시놉시스·본문·일관성 결과에 적용됩니다. `ExtractSettingsAsync`(설정+인물, JSON 객체 → `ExtractedSettings`), `ExtractSynopsisAsync`, `ExtractChaptersAsync`, 그리고 "전체 분석"(`AnalyzeAllAsync`: 설정→시놉시스→장→각 장 `GenerateScenesAsync`).
  - **장편 대응**: `CondenseAsync`가 12000자 초과 원고를 9000자 청크(문단 경계)로 나눠 각각 요약 → 압축본을 만들고(맵 단계), 이후 추출은 압축본으로 수행. "전체 분석"은 압축을 한 번만 하고 재사용. 진행 상황은 `IProgress<string>`→`StatusMessage`.
  - **덮어쓰기 확인**: 각 역분석 명령은 관련 기존 데이터가 있으면 `ConfirmOverwrite` 콜백(`StoryPlannerWindow`의 `MessageBox` Yes/No)으로 확인 후 진행. `ApplySettings`/`ApplyChapters`가 Clear→재생성.
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
- **문자 오프셋 통일**: 문단/줄바꿈을 모두 `\n` 1글자로 취급(`RichTextBoxHelpers`의 GetPlainText/GetPointerAtOffset/GetOffset). `StatisticsService`도 `\n` 기준. 오타 `TypoMark`의 offset과 어도너·우클릭·`VisibleRangeResolver`(`GetPositionFromPoint`→`GetOffset`)가 이 규칙을 공유합니다.
- **서식 툴바**(두 번째 툴바 행, 코드비하인드 이벤트): 글꼴/크기 ComboBox, 굵게·기울임·밑줄 토글(`Selection.ApplyPropertyValue`), 글자색·하이라이트(`ShowColorMenu` 팔레트 ContextMenu → Foreground/Background), 서식 지우기(`ClearAllProperties`).
- **DOCX 서식 저장**: `.docx`로 저장/내보내기 시 `DocxDocumentSaver` 콜백(View)이 `DocxExportService.ExportFlowDocumentAsync`로 **RichTextBox FlowDocument → DOCX 서식**(굵게·기울임·밑줄·글자색·크기·하이라이트)을 매핑해 저장합니다. **편집기 기본 24px = 11pt** 기준: `sz`(하프포인트) = `px * 11/12`. txt/md는 평문 저장.
- **편집기 폰트 크기**: `EditorFontSize`(기본 24px)를 설정 창 슬라이더로 조절, RichTextBox `FontSize`에 바인딩. 화면은 크게 보이되 DOCX 저장은 11pt로 나갑니다.
- **참고자료 생성기**(메뉴 "참고자료 생성기" → `ReferenceGeneratorWindow`/`ReferenceGeneratorViewModel`): 유형(캐릭터/세계관/시놉시스 등) + 요청으로 `ChatService`가 **마크다운(.md)** 생성 → 편집 후 `.md` 저장. 저장 후 참고자료 서랍을 새로고침합니다. 유형에 "묘사·표현 모음/감정·심리 묘사/배경·풍경 묘사/대사·문장 모음"이 있고, 이 계열은 `BuildSystemPrompt`가 **소설 문장 특화 프롬프트**(참신한 묘사·표현을 바로 쓸 수 있는 문장으로)로 전환합니다.

### 데이터/설정 경로

`%LocalAppData%\NovelWriter\` 아래에 저장됩니다: `novel_writer.db`(SQLite 문서), `settings.json`(`SettingsService`, `AppSettings` JSON), `Backups\`(`BackupService`).

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
- **설정 창**(상단 "설정" 메뉴 → `MainWindow.OnOpenSettings` → `SettingsWindow`): DataContext로 `MainViewModel`을 공유하며 세로 스크롤. 항목 = AI 모델(ComboBox), 자동 저장 on/off(`AutoSaveEnabled`→`UpdateAutoSaveTimer`)·주기(`AutoSaveSeconds`), 메뉴 폰트 크기(`MenuFontSize`→상단 `Menu.FontSize`), **툴바 아이콘 크기**(`ToolbarIconSize`→툴바 `StackPanel.Resources`의 `PackIcon` 암시적 스타일 Width/Height), 참고자료 폰트 크기(`ReferenceFontSize`)·색, **AI 어시스턴트 폰트 크기**(`ChatFontSize`→채팅 `DockPanel.TextElement.FontSize`)·**배경색**(`ChatBackgroundHex`→`ChatBackground`). "저장"이 `SaveSettingsCommand`(→`PersistSettingsAsync` + `EnsureAiReadyAsync` 재확인) 호출.
- **색 팔레트**: 색 설정은 hex 입력 + `PaletteColors` 스와치(ItemsControl)에서 선택. 스와치 클릭 → `SetReferenceColorCommand`/`SetChatBackgroundCommand`/`SetCustomBackgroundCommand`/`SetCustomForegroundCommand`(CommandParameter=hex). 스와치 배경은 `StringToBrushConverter`로 hex→Brush.
- **테마 커스텀**: 설정 창 하단에서 커스텀 배경/글자색을 hex+팔레트로 지정. `SetCustomBackground`/`SetCustomForeground`가 `Theme="Custom"` + `ApplyTheme()`로 즉시 반영(`CustomBackgroundHex`/`CustomForegroundHex`).
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
