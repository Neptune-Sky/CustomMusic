using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using SFS.Audio;
using UnityEngine;
using UnityEngine.Networking;
using Random = System.Random;
// ReSharper disable InconsistentNaming

namespace CustomMusic
{
    [HarmonyPatch(typeof(MusicPlaylistPlayer), "StartPlaying")]
    public static class Patch_StartPlaying
    {
        [UsedImplicitly]
        private static bool Prefix(MusicPlaylistPlayer __instance, float fadeTime)
        {
            // Prevent default logic and use ours
            return !TrackPlayer.TryPlayTrack(__instance, null, fadeTime);
        }
    }

    [HarmonyPatch(typeof(MusicPlaylistPlayer), "Update")]
    public static class Patch_MusicPlaylistPlayer_Update
    {
        private static void Postfix(MusicPlaylistPlayer __instance)
        {
            MusicPlaylist playlist = __instance.playlist;
            AudioSource source = __instance.source;

            if (playlist == null || source == null || !source.isPlaying)
                return;

            var currentTrack = ReflectionUtils.GetPrivateField<int>(__instance, "currentTrack");

            // Check if current track index is invalid
            if (currentTrack < 0 || currentTrack >= playlist.tracks.Count)
            {
                TrackPlayer.TryPlayTrack(__instance, null, 1f);
                return;
            }

            MusicTrack track = playlist.tracks[currentTrack];
            var isCustom = File.Exists(track.clipName);
            var allowVanilla = MusicInjector.ShouldIncludeVanilla(__instance.gameObject.scene.name);

            if (!isCustom && !allowVanilla)
            {
                TrackPlayer.TryPlayTrack(__instance, null, 1f);
            }
        }
    }
}