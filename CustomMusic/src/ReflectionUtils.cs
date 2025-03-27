using System.Reflection;
using SFS.Audio;
using UnityEngine;

namespace CustomMusic
{
    public static class ReflectionUtils
    {
        public static void SetPrivateField<T>(object obj, string fieldName, T value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"[CustomMusicMod] Failed to set private field '{fieldName}' on {obj}");
            }
        }
        
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            return (T)typeof(MusicPlaylistPlayer)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(obj);
        }
    }
}