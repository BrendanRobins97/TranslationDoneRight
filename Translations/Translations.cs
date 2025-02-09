namespace PSS
{
    public static class Translations
    {
        public static string Translate(string key)
        {
            return TranslationManager.Translate(key);
        }

        public static string Translate(string key, params (string name, object value)[] parameters)
        {
            return TranslationManager.Translate(key, parameters);
        }
    }
}