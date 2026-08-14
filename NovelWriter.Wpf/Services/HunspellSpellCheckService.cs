using System.IO;
using WeCantSpell.Hunspell;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// Hunspell 한국어 사전을 사용해 어절 맞춤법을 검사하고 추천을 제공합니다.
/// </summary>
public sealed class HunspellSpellCheckService
{
    private WordList? _wordList;

    /// <summary>
    /// 사전이 로드되어 검사 가능한 상태이면 true입니다.
    /// </summary>
    public bool IsReady => _wordList is not null;

    /// <summary>
    /// 실행 디렉터리의 Dictionaries 폴더에서 ko.aff/ko.dic을 비동기 로드합니다.
    /// </summary>
    /// <param name="dictionaryDirectory">사전 파일이 있는 폴더 경로입니다.</param>
    /// <returns>로드 성공 여부입니다.</returns>
    public async Task<bool> LoadAsync(string dictionaryDirectory)
    {
        var affPath = Path.Combine(dictionaryDirectory, "ko.aff");
        var dicPath = Path.Combine(dictionaryDirectory, "ko.dic");

        if (!File.Exists(affPath) || !File.Exists(dicPath))
        {
            return false;
        }

        try
        {
            // 11MB 규모의 aff 파싱은 무거우므로 백그라운드 스레드에서 로드합니다.
            _wordList = await Task.Run(() =>
            {
                using var dictionaryStream = File.OpenRead(dicPath);
                using var affixStream = File.OpenRead(affPath);
                return WordList.CreateFromStreams(dictionaryStream, affixStream);
            }).ConfigureAwait(false);

            return true;
        }
        catch
        {
            _wordList = null;
            return false;
        }
    }

    /// <summary>
    /// 어절이 사전에 있으면(정상) true를 반환합니다. 사전 미로드 시 항상 true입니다.
    /// </summary>
    public bool Check(string word)
    {
        if (_wordList is null || string.IsNullOrWhiteSpace(word))
        {
            return true;
        }

        return _wordList.Check(word);
    }

    /// <summary>
    /// 틀린 어절에 대한 교정 추천을 반환합니다.
    /// </summary>
    /// <param name="word">틀린 어절입니다.</param>
    /// <param name="max">최대 추천 개수입니다.</param>
    public IReadOnlyList<string> Suggest(string word, int max = 7)
    {
        if (_wordList is null || string.IsNullOrWhiteSpace(word))
        {
            return Array.Empty<string>();
        }

        // Hunspell 후보를 자모 단위 편집거리로 재정렬합니다(가까운 것 우선).
        // 동점이면 Hunspell 원래 순서를 유지합니다.
        return _wordList.Suggest(word)
            .Select((candidate, order) => (candidate, order))
            .OrderBy(x => HangulJamo.Distance(word, x.candidate))
            .ThenBy(x => x.order)
            .Select(x => x.candidate)
            .Take(max)
            .ToList();
    }
}
