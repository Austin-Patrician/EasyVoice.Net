namespace EasyVoice.Core.Constants;

/// <summary>
/// 语音常量定义
/// 提供各种 TTS 引擎支持的语音名称常量
/// </summary>
public static class VoiceConstants
{
    /// <summary>
    /// Microsoft Edge TTS 支持的语音常量
    /// </summary>
    public static class Edge
    {
        #region 中文语音
        /// <summary>
        /// 中文（中国大陆）- 晓伊，女声
        /// </summary>
        public const string ZH_CN_XIAOYI = "zh-CN-XiaoyiNeural";

        /// <summary>
        /// 中文（中国大陆）- 晓晓，女声
        /// </summary>
        public const string ZH_CN_XIAOXIAO = "zh-CN-XiaoxiaoNeural";

        /// <summary>
        /// 中文（中国大陆）- 云希，男声
        /// </summary>
        public const string ZH_CN_YUNXI = "zh-CN-YunxiNeural";

        /// <summary>
        /// 中文（中国大陆）- 云扬，男声
        /// </summary>
        public const string ZH_CN_YUNYANG = "zh-CN-YunyangNeural";
        #endregion

        #region 英文语音
        /// <summary>
        /// 英文（美国）- Aria，女声
        /// </summary>
        public const string EN_US_ARIA = "en-US-AriaNeural";

        /// <summary>
        /// 英文（美国）- Davis，男声
        /// </summary>
        public const string EN_US_DAVIS = "en-US-DavisNeural";

        /// <summary>
        /// 英文（美国）- Guy，男声
        /// </summary>
        public const string EN_US_GUY = "en-US-GuyNeural";

        /// <summary>
        /// 英文（美国）- Jane，女声
        /// </summary>
        public const string EN_US_JANE = "en-US-JaneNeural";
        #endregion

        #region 日文语音
        /// <summary>
        /// 日文（日本）- Nanami，女声
        /// </summary>
        public const string JA_JP_NANAMI = "ja-JP-NanamiNeural";

        /// <summary>
        /// 日文（日本）- Keita，男声
        /// </summary>
        public const string JA_JP_KEITA = "ja-JP-KeitaNeural";
        #endregion
    }

    /// <summary>
    /// OpenAI TTS 支持的语音常量
    /// </summary>
    public static class OpenAI
    {
        /// <summary>
        /// Alloy 语音 - 平衡的中性音色
        /// </summary>
        public const string ALLOY = "alloy";

        /// <summary>
        /// Echo 语音 - 男性音色
        /// </summary>
        public const string ECHO = "echo";

        /// <summary>
        /// Fable 语音 - 英式口音
        /// </summary>
        public const string FABLE = "fable";

        /// <summary>
        /// Onyx 语音 - 深沉的男性音色
        /// </summary>
        public const string ONYX = "onyx";

        /// <summary>
        /// Nova 语音 - 年轻的女性音色
        /// </summary>
        public const string NOVA = "nova";

        /// <summary>
        /// Shimmer 语音 - 温和的女性音色
        /// </summary>
        public const string SHIMMER = "shimmer";
    }

    /// <summary>
    /// 豆包 TTS 支持的语音常量
    /// </summary>
    public static class Doubao
    {
        #region 中文语音
        /// <summary>
        /// 中文女声 1
        /// </summary>
        public const string ZH_FEMALE_1 = "zh_female_1";

        /// <summary>
        /// 中文女声 2
        /// </summary>
        public const string ZH_FEMALE_2 = "zh_female_2";

        /// <summary>
        /// 中文男声 1
        /// </summary>
        public const string ZH_MALE_1 = "zh_male_1";

        /// <summary>
        /// 中文男声 2
        /// </summary>
        public const string ZH_MALE_2 = "zh_male_2";
        #endregion

        #region 英文语音
        /// <summary>
        /// 英文女声 1
        /// </summary>
        public const string EN_FEMALE_1 = "en_female_1";

        /// <summary>
        /// 英文男声 1
        /// </summary>
        public const string EN_MALE_1 = "en_male_1";
        #endregion
    }

    /// <summary>
    /// Kokoro TTS 支持的语音常量
    /// </summary>
    public static class Kokoro
    {
        /// <summary>
        /// Kokoro 默认日文语音
        /// </summary>
        public const string KOKORO = "kokoro";
    }
}
