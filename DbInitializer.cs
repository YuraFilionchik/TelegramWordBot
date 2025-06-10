using Dapper;
using System.Data;

namespace TelegramWordBot;

public static class DatabaseInitializer
{
    public static async Task EnsureTablesAsync(DbConnectionFactory factory)
    {
        using var connection = factory.CreateConnection();

        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS languages (
                id SERIAL PRIMARY KEY,
                code TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL UNIQUE
            );",
            @"CREATE TABLE IF NOT EXISTS users (
                id UUID PRIMARY KEY,
                telegram_id BIGINT NOT NULL UNIQUE,
                native_language TEXT,
                current_language TEXT,
                prefer_multiple_choice BOOLEAN NOT NULL DEFAULT FALSE
            );",
            @"CREATE TABLE IF NOT EXISTS words (
                id UUID PRIMARY KEY,
                base_text TEXT NOT NULL,
                language_id INTEGER REFERENCES languages(id),
                UNIQUE(base_text, language_id)
            );",
            @"CREATE TABLE IF NOT EXISTS translations (
                id UUID PRIMARY KEY,
                word_id UUID REFERENCES words(id) ON DELETE CASCADE,
                language_id INTEGER REFERENCES languages(id),
                text TEXT NOT NULL,
                examples TEXT
            );",
            @"CREATE TABLE IF NOT EXISTS user_words (
                user_id UUID REFERENCES users(id) ON DELETE CASCADE,
                word_id UUID REFERENCES words(id) ON DELETE CASCADE,
                translation_id UUID REFERENCES translations(id) ON DELETE SET NULL,
                PRIMARY KEY(user_id, word_id)
            );",
            @"CREATE TABLE IF NOT EXISTS user_word_progress (
                id UUID PRIMARY KEY,
                user_id UUID REFERENCES users(id) ON DELETE CASCADE,
                word_id UUID REFERENCES words(id) ON DELETE CASCADE,
                repetition INTEGER NOT NULL DEFAULT 0,
                interval_hours INTEGER NOT NULL DEFAULT 0,
                ease_factor DOUBLE PRECISION NOT NULL DEFAULT 2.5,
                next_review TIMESTAMPTZ NOT NULL,
                last_review TIMESTAMPTZ,
                UNIQUE(user_id, word_id) 
            );",
            @"CREATE TABLE IF NOT EXISTS user_languages (
                user_id UUID REFERENCES users(id) ON DELETE CASCADE,
                language_id INTEGER REFERENCES languages(id) ON DELETE CASCADE,
                PRIMARY KEY(user_id, language_id)
            );",
            @"CREATE TABLE IF NOT EXISTS word_images (
                id UUID PRIMARY KEY,
                word_id UUID REFERENCES words(id) ON DELETE CASCADE,
                file_path TEXT NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS dictionaries (
                id UUID PRIMARY KEY,
                user_id UUID REFERENCES users(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                UNIQUE(user_id, name)
            );",
            @"CREATE TABLE IF NOT EXISTS dictionary_words (
                dictionary_id UUID REFERENCES dictionaries(id) ON DELETE CASCADE,
                word_id UUID REFERENCES words(id) ON DELETE CASCADE,
                PRIMARY KEY(dictionary_id, word_id)
            );",
            @"CREATE TABLE IF NOT EXISTS todo_items (
                id UUID PRIMARY KEY,
                user_id UUID REFERENCES users(id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                description TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL,
                is_complete BOOLEAN NOT NULL DEFAULT FALSE
            );"
        };

        foreach (var cmd in commands)
        {
            await connection.ExecuteAsync(cmd);
        }
    }

    public static async Task EnsureLanguagesAsync(DbConnectionFactory factory)
    {
        using var connection = factory.CreateConnection();

        var languages = new (string Code, string Name)[]
        {
            ("en", "English"),
            ("zh", "Chinese"),
            ("hi", "Hindi"),
            ("es", "Spanish"),
            ("fr", "French"),
            ("ar", "Arabic"),
            ("bn", "Bengali"),
            ("ru", "Russian"),
            ("pt", "Portuguese"),
            ("id", "Indonesian"),
            ("ur", "Urdu"),
            ("de", "German"),
            ("ja", "Japanese"),
            ("sw", "Swahili"),
            ("mr", "Marathi"),
            ("te", "Telugu"),
            ("tr", "Turkish"),
            ("ta", "Tamil"),
            ("vi", "Vietnamese"),
            ("ko", "Korean"),
            ("it", "Italian"),
            ("pl", "Polish"),
            ("uk", "Ukrainian"),
            ("nl", "Dutch"),
            ("gu", "Gujarati"),
            ("fa", "Persian"),
            ("ml", "Malayalam"),
            ("th", "Thai"),
            ("fil", "Filipino"),
            ("my", "Burmese"),
            ("eo", "Esperanto"),
        };

        foreach (var lang in languages)
        {
            await connection.ExecuteAsync(
                "INSERT INTO languages (code, name) VALUES (@Code, @Name) ON CONFLICT DO NOTHING",
                new { Code = lang.Code, Name = lang.Name });
        }
    }
}
