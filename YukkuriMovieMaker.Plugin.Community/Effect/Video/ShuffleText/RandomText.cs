namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ShuffleText
{
    public enum CharType
    {
        Alphabet,
        Number,
        Symbol,
        Custom
    }

    internal class RandomText
    {
        public static string Generate(CharType type, Random rand, ShuffleTextEffect item)
        {
            switch (type)
            {
                case CharType.Alphabet:
                    {
                        int ascii = rand.Next(2) == 0
                            ? rand.Next(65, 91)     // A-Z
                            : rand.Next(97, 123);   // a-z
                        return ((char)ascii).ToString();
                    }

                case CharType.Number:
                    {
                        int num = rand.Next(0, 10); // 0〜9
                        return num.ToString();
                    }

                case CharType.Symbol:
                    {
                        string symbols = "!@#$%^&*()-_=+[]{};:'\",.<>?/\\|`~";
                        return symbols[rand.Next(symbols.Length)].ToString();
                    }

                case CharType.Custom:
                    {
                        var builder = new List<string>();

                        //アルファベット大文字
                        if (item.UpLetter)
                            for (char c = 'A'; c <= 'Z'; c++) builder.Add(c.ToString());
                        //アルファベット小文字
                        if (item.LowLetter)
                            for (char c = 'a'; c <= 'z'; c++) builder.Add(c.ToString());
                        //ひらがな
                        if(item.Hirakana)
                            for (int i = 0x3041; i <= 0x3096; i++)
                            {
                                builder.Add(char.ConvertFromUtf32(i));
                            }
                        //カタカナ
                        if(item.Katakana)
                            for (int i = 0x30A1; i <= 0x30FA; i++)
                            {
                                builder.Add(char.ConvertFromUtf32(i));
                            }
                        //漢字
                        if(item.Kanji)
                            for (int i = 0; i < 10; i++)
                            {
                                int code = rand.Next(0x4E00, 0x9FFF + 1);
                                builder.Add(char.ConvertFromUtf32(code));
                            }
                        //数字
                        if(item.Number)
                            for (int i = 0; i <= 9; i++)
                            {
                                builder.Add(i.ToString());
                            }
                        //記号
                        if (item.Symbol)
                        {
                            string symbols = "!@#$%^&*()-_=+[]{};:'\",.<>?/\\|`~";
                            builder.AddRange(symbols.Select(c => c.ToString()));
                        }

                        //テキスト
                        if(!string.IsNullOrEmpty(item.Text))
                        {
                            foreach (var c in item.Text)
                                builder.Add(c.ToString());
                        }

                        if (builder.Count > 0)
                        {
                            return builder[rand.Next(builder.Count)];
                        }
                        return "?";
                    }

                default:
                    return "?";
            }
        }
    }
}
