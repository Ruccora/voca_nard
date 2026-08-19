namespace VocaNerd
{
    /// <summary>
    /// BGM のキー定数。<see cref="AudioLibrary"/> の Bgm Entries の Key と一致させる。
    /// 新しい BGM を増やすときはここに定数を足し、AudioLibrary.asset に同じキーの行を追加して
    /// AudioClip をアサインする。
    /// </summary>
    public static class BgmKey
    {
        public const string Title = "bgm_title";
        public const string Select = "bgm_select";
        public const string QuickDraw = "bgm_quickdraw";
        public const string MashRace = "bgm_mashrace";
        public const string HopscotchRace = "bgm_hopscotch";
        public const string BlockDrop = "bgm_blockdrop";

        /// <summary>AudioLibrary.asset を自動生成するときに並べる既定のキー一覧。</summary>
        public static readonly string[] All =
        {
            Title,
            Select,
            QuickDraw,
            MashRace,
            HopscotchRace,
            BlockDrop,
        };
    }

    /// <summary>
    /// SE のキー定数。<see cref="AudioLibrary"/> の Se Entries の Key と一致させる。
    /// </summary>
    public static class SeKey
    {
        /// <summary>決定音。UI ボタン押下時の既定 SE（<see cref="PanelBase"/> が自動再生）。</summary>
        public const string Decide = "se_decide";

        /// <summary>キャンセル音。Back 系ボタンに <see cref="ButtonSeKey"/> で割り当てる。</summary>
        public const string Cancel = "se_cancel";

        /// <summary>カーソル移動音。<see cref="SelectionIndicator"/> が選択変化時に自動再生。</summary>
        public const string Cursor = "se_cursor";

        // 以下はミニゲーム側から任意で呼ぶ用（自動再生はされない）
        public const string Countdown = "se_countdown";
        public const string Start = "se_start";
        public const string Win = "se_win";
        public const string Lose = "se_lose";
        public const string Miss = "se_miss";
        public const string Hit = "se_hit";

        /// <summary>AudioLibrary.asset を自動生成するときに並べる既定のキー一覧。</summary>
        public static readonly string[] All =
        {
            Decide,
            Cancel,
            Cursor,
            Countdown,
            Start,
            Win,
            Lose,
            Miss,
            Hit,
        };
    }
}
