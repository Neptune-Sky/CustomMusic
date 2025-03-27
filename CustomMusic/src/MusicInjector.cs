using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SFS.Audio;
using SFS.Variables;
using UnityEngine;

namespace CustomMusic
{
    public static class MusicInjector
    {
        public static void Inject(MusicPlaylistPlayer player, string sceneName)
        {
            if (!player || !player.playlist)
                return;
            
            var playlist = player.playlist;
            var customTracks = CustomMusicLoader.LoadForScene(sceneName);

            playlist.tracks = playlist.tracks
                .Where(t => !File.Exists(t.clipName)) // keep vanilla only
                .ToList();

            if (ShouldIncludeVanilla(sceneName))
            {
                var cachedVanilla = VanillaPlaylistCache.GetCachedVanilla(playlist);
                playlist.tracks = cachedVanilla.Concat(customTracks).ToList();
            }
            else
            {
                playlist.tracks = customTracks;
            }

            typeof(MusicPlaylistPlayer).GetField("currentTrack", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(player, -1);

        }
        
        public static IEnumerator InjectAfterSceneLoad(string sceneName)
        {

            MusicPlaylistPlayer player = null;

            // Wait until MusicPlaylistPlayer is present in the scene
            yield return new WaitUntil(() =>
            {
                player = UnityEngine.Object.FindObjectOfType<MusicPlaylistPlayer>();
                return player != null;
            });
            
            VanillaPlaylistCache.CacheIfNeeded(player.playlist);

            // Inject the correct playlist before StartPlaying is triggered
            Inject(player, sceneName);

            // Log final playlist for debugging
            var playlist = player.playlist;
            for (int i = 0; i < playlist.tracks.Count; i++)
            {
                var t = playlist.tracks[i];
                bool isCustom = File.Exists(t.clipName);
            }
        }
        public static bool ShouldIncludeVanilla(string sceneName) => sceneName switch
        {
            "Home_PC" => Config.settings.homeMenu.Value,
            "Build_PC" => Config.settings.buildScene.Value,
            "World_PC" => Config.settings.worldScene.Value,
            _ => true
        };

        public static void OnSceneToggleChanged(string sceneName)
        {
            var players = UnityEngine.Object.FindObjectsOfType<MusicPlaylistPlayer>();
            foreach (var p in players)
            {
                if (p != null && p.playlist != null && p.gameObject.scene.name == sceneName)
                {
                    Inject(p, sceneName);
                }
            }
        }
    }
}