using System.Data.SQLite;
using System.IO;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// SQLite 기반 문서 저장소를 제공합니다.
/// </summary>
public sealed class DocumentRepository
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    /// <summary>
    /// 저장소를 초기화합니다.
    /// </summary>
    /// <param name="dataDirectory">데이터 디렉터리 경로입니다.</param>
    public DocumentRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _dbPath = Path.Combine(dataDirectory, "novel_writer.db");
        _connectionString = $"Data Source={_dbPath};Version=3;";
    }

    /// <summary>
    /// 데이터베이스 테이블을 생성합니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Documents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 문서를 저장합니다.
    /// </summary>
    /// <param name="title">문서 제목입니다.</param>
    /// <param name="content">문서 본문입니다.</param>
    public async Task SaveAsync(string title, string content)
    {
        await using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = "DELETE FROM Documents;";
        await deleteCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO Documents (Title, Content, UpdatedAt)
            VALUES (@title, @content, @updatedAt);
            """;
        insertCommand.Parameters.AddWithValue("@title", string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim());
        insertCommand.Parameters.AddWithValue("@content", content ?? string.Empty);
        insertCommand.Parameters.AddWithValue("@updatedAt", DateTimeOffset.Now.ToString("O"));
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 마지막 문서를 불러옵니다.
    /// </summary>
    /// <returns>제목과 내용을 반환합니다.</returns>
    public async Task<(string Title, string Content)> LoadLatestAsync()
    {
        await using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Title, Content
            FROM Documents
            ORDER BY Id DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return (reader.GetString(0), reader.GetString(1));
        }

        return ("새 문서", string.Empty);
    }
}
