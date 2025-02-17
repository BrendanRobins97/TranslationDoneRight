#if UNITY_EDITOR
namespace PSS
{
    public enum KeyUpdateMode
    {
        Replace,  // Clear existing and add new keys
        Merge     // Keep existing and add new keys
    }
}
#endif

