using System.IO;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 사용자가 "사전에 추가"한 단어를 파일로 관리합니다. (Hunspell 검사보다 먼저 확인)
/// </summary>
public sealed class UserDictionaryService
{
    private readonly string _path;
    private readonly HashSet<string> _words = new(StringComparer.Ordinal);

    /// <summary>
    /// 사용자 사전 서비스를 초기화하고 기존 단어를 읽어옵니다.
    /// </summary>
    /// <param name="dataDirectory">데이터 디렉터리 경로입니다.</param>
    public UserDictionaryService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "user_dictionary.txt");

        if (File.Exists(_path))
        {
            foreach (var line in File.ReadAllLines(_path))
            {
                var word = line.Trim();
                if (word.Length > 0)
                {
                    _words.Add(word);
                }
            }
        }
    }

    /// <summary>
    /// 단어가 사용자 사전에 있는지 확인합니다.
    /// </summary>
    public bool Contains(string word) => _words.Contains(word);

    /// <summary>
    /// 단어를 사용자 사전에 추가하고 파일에 저장합니다.
    /// </summary>
    public void Add(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || !_words.Add(word))
        {
            return;
        }

        try
        {
            File.AppendAllText(_path, word + Environment.NewLine);
        }
        catch (IOException)
        {
            // 저장 실패는 무시합니다(메모리에는 반영됨).
        }
    }
}
