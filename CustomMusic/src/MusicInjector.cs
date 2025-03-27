using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SFS.Audio;
using UnityEngine;

namespace CustomMusic
{
    public static class MusicInjector
    {
        private static void Inject(MusicPlaylistPlayer player, string sceneName)
        {
            if (!player || !player.playlist)
                return;

            MusicPlaylist playlist = player.playlist;
            var customTracks = MusicLoader.LoadForScene(sceneName);

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
            Debug.Log($"[CustomMusicMod] Waiting for MusicPlaylistPlayer in scene: {sceneName}");

            MusicPlaylistPlayer player = null;
            yield return new WaitUntil(() =>
            {
                player = Object.FindObjectOfType<MusicPlaylistPlayer>();
                return player;
            });

            if (!player || !player.playlist)
            {
                Debug.LogWarning("[CustomMusicMod] Aborted: player or playlist became null after wait");
                yield break;
            }

            // Prevent duplicate inject
            if (player.playlist.tracks.Count > 0 &&
                player.playlist.tracks.Any(t => File.Exists(t.clipName)))
            {
                Debug.Log("[CustomMusicMod] Skipping inject: playlist already contains custom tracks");
                yield break;
            }

            VanillaPlaylistCache.CacheIfNeeded(player.playlist);
            Inject(player, sceneName);

            Debug.Log($"[CustomMusicMod] Final injected playlist: {player.playlist.tracks.Count} tracks");
        }

        public static bool ShouldIncludeVanilla(string sceneName)
        {
            return sceneName switch
            {
                "Home_PC" => Config.settings.homeMenu.Value,
                "Build_PC" => Config.settings.buildScene.Value,
                "World_PC" => Config.settings.worldScene.Value,
                _ => true
            };
        }

        public static void OnSceneToggleChanged(string sceneName)
        {
            var players = Object.FindObjectsOfType<MusicPlaylistPlayer>();
            foreach (MusicPlaylistPlayer p in players)
                if (p != null && p.playlist != null && p.gameObject.scene.name == sceneName)
                    Inject(p, sceneName);
        }
    }
}