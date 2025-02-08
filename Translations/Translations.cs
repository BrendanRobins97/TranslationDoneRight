namespace PSS
{
    public static class Translations
    {
        public static string Translate(string key)
        {
            return TranslationManager.Translate(key);
        }
    }
}