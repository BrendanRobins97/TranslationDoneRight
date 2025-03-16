#if UNITY_EDITOR
using UnityEditor;

namespace Translations
{
    [InitializeOnLoad]
    public class UpdateChecker
    {
        static UpdateChecker()
        {
            // Add a small delay to ensure everything is initialized
            EditorApplication.delayCall += () =>
            {
                VersionManager.ShowUpdateNotificationIfNeeded();
            };
        }
    }
}
#endif 