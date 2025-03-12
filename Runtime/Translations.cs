namespace Translations
{
    public static class Translations
    {
        public static string Translate(string key)
        {
            return TranslationManager.Translate(key);
        }

        public static string TranslateString(this string text)
        {
            return TranslationManager.Translate(text);
        }

        public static string SmartTranslate(this string text)
        {
            return TranslationManager.TranslateSmart(text);
        }

        public static string Format(string format, params object[] args)
        {
            // Translate the format string
            string translatedFormat = TranslationManager.Translate(format);
            
            // Translate any string arguments
            object[] translatedArgs = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is string stringArg)
                {
                    translatedArgs[i] = TranslationManager.Translate(stringArg);
                }
                else
                {
                    translatedArgs[i] = args[i];
                }
            }
            
            return string.Format(translatedFormat, translatedArgs);
        }
    }
}