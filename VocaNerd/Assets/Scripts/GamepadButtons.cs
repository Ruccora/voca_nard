namespace VocaNerd
{
    /// <summary>
    /// パッドのボタンを「印刷されたラベル」で指すためのバインドパス。
    ///
    /// Unity の control 名は **位置** で決まる (buttonSouth = 下 / buttonEast = 右)。
    /// Xbox パッドは下に A と書いてあるので世間では「A = buttonSouth」で通っているが、
    /// このプロジェクトで使うパッドは **A が右 / B が下** の任天堂式ラベルなので、
    /// そのまま buttonSouth を使うと「B と書いてあるボタンで決定」になってしまう。
    ///
    /// そのため全ミニゲームと UI はここ経由でバインドする。
    /// 別ラベルのパッドに乗り換えるときは **この 2 行だけ** 直せばよい。
    /// </summary>
    public static class GamepadButtons
    {
        /// <summary>「A」と印刷されたボタン。決定 / 主操作。</summary>
        public const string A = "<Gamepad>/buttonEast";

        /// <summary>「B」と印刷されたボタン。キャンセル / 副操作。</summary>
        public const string B = "<Gamepad>/buttonSouth";
    }
}
