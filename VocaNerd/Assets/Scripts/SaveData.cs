using System;
using UnityEngine;

namespace VocaNerd
{
    /// <summary>
    /// PlayerPrefs をラップしたセーブデータ管理クラス。
    /// ゲームごとのハイスコア保持を中心に、汎用的な型別 API も提供する。
    /// すべて静的メソッドで、どこからでも呼び出せる。
    /// </summary>
    public static class SaveData
    {
        // PlayerPrefs のキー衝突を避けるための接頭辞
        private const string Prefix = "VocaNerd.";
        private const string HighScorePrefix = Prefix + "HighScore.";
        private const string BestTimePrefix = Prefix + "BestTime.";
        private const string PlayCountPrefix = Prefix + "PlayCount.";

        /// <summary>各ミニゲームのセーブキーに使う識別子。</summary>
        public static class GameId
        {
            public const string BlockDrop = "BlockDrop";
            public const string HopscotchRace = "HopscotchRace";
            public const string MashRace = "MashRace";
        }

        // ---------------- ハイスコア ----------------

        /// <summary>指定ゲームのハイスコアを取得する。未保存なら 0。</summary>
        public static int GetHighScore(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return 0;
            return PlayerPrefs.GetInt(HighScorePrefix + gameId, 0);
        }

        /// <summary>
        /// スコアを記録し、既存ハイスコアを上回っていれば更新する。
        /// </summary>
        /// <returns>ハイスコアを更新した場合 true。</returns>
        public static bool TrySetHighScore(string gameId, int score)
        {
            if (string.IsNullOrEmpty(gameId)) return false;

            var current = GetHighScore(gameId);
            if (score <= current) return false;

            PlayerPrefs.SetInt(HighScorePrefix + gameId, score);
            PlayerPrefs.Save();
            return true;
        }

        // ---------------- ベストタイム（小さいほど良い） ----------------

        /// <summary>指定ゲームのベストタイム（秒）が保存されているか。</summary>
        public static bool HasBestTime(string gameId)
            => !string.IsNullOrEmpty(gameId) && PlayerPrefs.HasKey(BestTimePrefix + gameId);

        /// <summary>
        /// 指定ゲームのベストタイム（秒）を取得する。未保存なら defaultValue。
        /// </summary>
        public static float GetBestTime(string gameId, float defaultValue = 0f)
        {
            if (string.IsNullOrEmpty(gameId)) return defaultValue;
            return PlayerPrefs.GetFloat(BestTimePrefix + gameId, defaultValue);
        }

        /// <summary>
        /// クリアタイムを記録し、未保存または既存ベストより速ければ更新する。
        /// </summary>
        /// <returns>ベストタイムを更新した場合 true。</returns>
        public static bool TrySetBestTime(string gameId, float seconds)
        {
            if (string.IsNullOrEmpty(gameId) || seconds < 0f) return false;

            if (HasBestTime(gameId) && seconds >= GetBestTime(gameId)) return false;

            PlayerPrefs.SetFloat(BestTimePrefix + gameId, seconds);
            PlayerPrefs.Save();
            return true;
        }

        // ---------------- プレイ回数 ----------------

        /// <summary>指定ゲームの累計プレイ回数を取得する。</summary>
        public static int GetPlayCount(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return 0;
            return PlayerPrefs.GetInt(PlayCountPrefix + gameId, 0);
        }

        /// <summary>指定ゲームのプレイ回数を 1 増やして保存する。</summary>
        /// <returns>加算後のプレイ回数。</returns>
        public static int IncrementPlayCount(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return 0;

            var next = GetPlayCount(gameId) + 1;
            PlayerPrefs.SetInt(PlayCountPrefix + gameId, next);
            PlayerPrefs.Save();
            return next;
        }

        // ---------------- 汎用 API ----------------

        public static int GetInt(string key, int defaultValue = 0)
            => PlayerPrefs.GetInt(Prefix + key, defaultValue);

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static float GetFloat(string key, float defaultValue = 0f)
            => PlayerPrefs.GetFloat(Prefix + key, defaultValue);

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static string GetString(string key, string defaultValue = "")
            => PlayerPrefs.GetString(Prefix + key, defaultValue);

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static bool GetBool(string key, bool defaultValue = false)
            => PlayerPrefs.GetInt(Prefix + key, defaultValue ? 1 : 0) != 0;

        public static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ---------------- 管理 ----------------

        /// <summary>指定キーが保存されているか。</summary>
        public static bool HasKey(string key)
            => PlayerPrefs.HasKey(Prefix + key);

        /// <summary>指定ゲームのハイスコアとプレイ回数を削除する。</summary>
        public static void ClearGame(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return;
            PlayerPrefs.DeleteKey(HighScorePrefix + gameId);
            PlayerPrefs.DeleteKey(BestTimePrefix + gameId);
            PlayerPrefs.DeleteKey(PlayCountPrefix + gameId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// VocaNerd のセーブデータをすべて削除する。
        /// PlayerPrefs.DeleteAll と異なり、接頭辞付きキーのみを対象にはできないため、
        /// 他アプリ設定と混在しないよう注意して使うこと（このアプリ専用ビルド前提）。
        /// </summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
