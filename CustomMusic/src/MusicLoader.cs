using System.Collections.Generic;
using System.IO;
using System.Linq;
using SFS.Audio;
using UnityEngine;

namespace CustomMusic
{
    public static class MusicLoader
    {
        private static readonly string MusicBasePath = Path.Combine(Main.modFolder, "Music");
        private static readonly string[] SupportedExt = { ".mp3", ".wav", ".ogg", ".aiff" };

        public static List<MusicTrack> LoadForScene(string sceneName)
        {
            var path = Path.Combine(MusicBasePath, sceneName);
            if (!Directory.Exists(path)) return new List<MusicTrack>();

            return Directory.GetFiles(path)
                .Where(f => SupportedExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new MusicTrack
                {
                    clipName = f, // full path = trigger custom load
                    volume = 1f,
                    pitch = 1f,
                    onTrackEnd = MusicTrack.OnTrackEnd.PlayNext
                })
                .ToList();
        }
    }

    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (!instance)
                    Create();
                return instance;
            }
        }

        public static void Create()
        {
            if (instance) return;

            var go = new GameObject("CustomMusicMod_CoroutineRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CoroutineRunner>();
        }
    }
}