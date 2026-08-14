using System.Text;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 한글 음절을 자모(초성/중성/종성)로 분해하고 자모 단위 편집거리를 계산합니다.
/// (SymSpell식 자모 근접 추천에 사용)
/// </summary>
public static class HangulJamo
{
    private const int SyllableBase = 0xAC00;
    private const int SyllableLast = 0xD7A3;
    private const int JungCount = 21;
    private const int JongCount = 28;

    private static readonly char[] Cho =
        "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ".ToCharArray();

    private static readonly char[] Jung =
        "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ".ToCharArray();

    // 인덱스 0은 "종성 없음"이며 사용되지 않습니다.
    private static readonly char[] Jong =
        "_ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ".ToCharArray();

    /// <summary>
    /// 문자열의 한글 음절을 자모열로 분해합니다. (한글이 아니면 그대로 유지)
    /// </summary>
    public static string Decompose(string text)
    {
        var builder = new StringBuilder(text.Length * 3);

        foreach (var ch in text)
        {
            if (ch >= SyllableBase && ch <= SyllableLast)
            {
                var index = ch - SyllableBase;
                var cho = index / (JungCount * JongCount);
                var jung = index % (JungCount * JongCount) / JongCount;
                var jong = index % JongCount;

                builder.Append(Cho[cho]);
                builder.Append(Jung[jung]);
                if (jong != 0)
                {
                    builder.Append(Jong[jong]);
                }
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 두 단어를 자모로 분해한 뒤의 편집거리(Levenshtein)를 반환합니다. 작을수록 유사합니다.
    /// </summary>
    public static int Distance(string a, string b)
    {
        return Levenshtein(Decompose(a), Decompose(b));
    }

    private static int Levenshtein(string s, string t)
    {
        if (s.Length == 0)
        {
            return t.Length;
        }

        if (t.Length == 0)
        {
            return s.Length;
        }

        var previous = new int[t.Length + 1];
        var current = new int[t.Length + 1];

        for (var j = 0; j <= t.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= s.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= t.Length; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[t.Length];
    }
}
