namespace SmartSpace.Character
{
    public enum EmoteType : sbyte
    {
        None = 0,
        Walk = 1,
        Run = 2,
        Jump = 3,
        Wave = 10,       // 👋 热情挥手
        Heart = 11,      // 💖 飞吻比心
        Clap = 12,       // 👏 鼓掌喝彩
        Bow = 13,        // 🙇 优雅鞠躬
        Dance = 14,      // 🕺 欢快摇摆
        HighFive = 15    // ✋ 伸手击掌
    }

    public static class EmoteHelper
    {
        public static string GetName(EmoteType type) => type switch
        {
            EmoteType.Wave => "挥手致意",
            EmoteType.Heart => "飞吻比心",
            EmoteType.Clap => "鼓掌喝彩",
            EmoteType.Bow => "优雅鞠躬",
            EmoteType.Dance => "欢快摇摆",
            EmoteType.HighFive => "伸手击掌",
            _ => "打招呼"
        };

        public static string GetEmoji(EmoteType type) => type switch
        {
            EmoteType.Wave => "👋",
            EmoteType.Heart => "💖",
            EmoteType.Clap => "👏",
            EmoteType.Bow => "🙇",
            EmoteType.Dance => "🕺",
            EmoteType.HighFive => "✋",
            _ => "✨"
        };

        public static string GetBadgeText(EmoteType type) => type switch
        {
            EmoteType.Wave => "<color=#FFE082><b>[ WAVE ]</b></color>",
            EmoteType.Heart => "<color=#FF4081><b><3 LOVE <3</b></color>",
            EmoteType.Clap => "<color=#69F0AE><b>*CLAP!*</b></color>",
            EmoteType.Bow => "<color=#80D8FF><b>*BOW*</b></color>",
            EmoteType.Dance => "<color=#E040FB><b>~ DANCE ~</b></color>",
            EmoteType.HighFive => "<color=#FFAB40><b>\\o/ HI-FIVE!</b></color>",
            _ => "<color=#FFFFFF><b>[ HELLO ]</b></color>"
        };

        public static string GetChatText(EmoteType type) => type switch
        {
            EmoteType.Wave => "热情地向大家挥了挥手 👋",
            EmoteType.Heart => "送出了一颗闪耀的爱心 💖",
            EmoteType.Clap => "兴奋地鼓掌喝彩 👏",
            EmoteType.Bow => "彬彬有礼地深情鞠躬 🙇",
            EmoteType.Dance => "在广场中央欢快地跳起舞来 🕺",
            EmoteType.HighFive => "高高举起右手求击掌 ✋",
            _ => "向周围的人致意 ✨"
        };
    }
}
