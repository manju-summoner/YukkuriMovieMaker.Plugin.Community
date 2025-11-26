namespace YukkuriMovieMaker.Plugin.Community.Voice.Kotodama.API
{
    record KotodamaDefaultDecoration(string Id, string Name)
    {
        public static KotodamaDefaultDecoration Neutral { get; } = new("neutral", "普通");
        public static KotodamaDefaultDecoration Happy { get; } = new("happy", "喜び");
        public static KotodamaDefaultDecoration Angry { get; } = new("angry", "怒り");
        public static KotodamaDefaultDecoration Sad { get; } = new("sad", "悲しみ");
        public static KotodamaDefaultDecoration Surprised { get; } = new("surprised", "驚き");
        public static KotodamaDefaultDecoration Laughing { get; } = new("laughing", "笑い");
        public static KotodamaDefaultDecoration Scared { get; } = new("scared", "恐れ");
        public static KotodamaDefaultDecoration Confused { get; } = new("confused", "混乱");
        public static KotodamaDefaultDecoration Disgusted { get; } = new("disgusted", "嫌悪");
        public static KotodamaDefaultDecoration Friendly { get; } = new("friendly", "親しみ");
        public static KotodamaDefaultDecoration Interested { get; } = new("interested", "興味");
        public static KotodamaDefaultDecoration Crying { get; } = new("crying", "泣き");
        public static KotodamaDefaultDecoration LaughingSpeech { get; } = new("laughing_speech", "笑いながら話す");
        public static KotodamaDefaultDecoration CryingSpeech { get; } = new("crying_speech", "泣きながら話す");
        public static KotodamaDefaultDecoration VeryHappy { get; } = new("very_happy", "大喜び");
        public static KotodamaDefaultDecoration CreateEnglish(KotodamaDefaultDecoration decoration) => new(decoration.Id + "_en", decoration.Name + "（英語）");

        static KotodamaDefaultDecoration[] CharacterDefaultDecorationsJa { get; } = [
            Neutral, Happy, Angry,
            Sad, Surprised, Laughing,
            Scared, Confused, Disgusted,
            Friendly, Interested, Crying,
            LaughingSpeech, CryingSpeech,];
        public static KotodamaDefaultDecoration[] CharacterDefaultDecorations { get; } = ConcatEnglishDecorations(CharacterDefaultDecorationsJa);
        public static KotodamaDefaultDecoration[] ConcatEnglishDecorations(KotodamaDefaultDecoration[] decorations) => [.. decorations, .. decorations.Select(CreateEnglish)];
    }
}
