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

            var currentTrack = GetPrivateField<int>(__instance, "currentTrack");

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

        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            return (T)typeof(MusicPlaylistPlayer)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(obj);
        }
    }

    public static class TrackPlayer
    {
        private static bool isSwitchingTracks;
        private static readonly Random Rng = new();

        public static bool TryPlayTrack(MusicPlaylistPlayer player, int? requestedIndex, float fadeTime)
        {
            if (isSwitchingTracks)
            {
                return false;
            }

            isSwitchingTracks = true;
            
            MusicPlaylist playlist = player.playlist;
            if (!playlist || playlist.tracks.Count == 0)
            {
                isSwitchingTracks = false;
                return false;
            }

            var lastTrack = GetCurrentTrack(player);
            var index = requestedIndex ?? GetNextValidTrack(player, lastTrack);

            if (index < 0 || index >= playlist.tracks.Count)
            {
                isSwitchingTracks = false;
                return false;
            }

            MusicTrack track = playlist.tracks[index];
            var isCustom = File.Exists(track.clipName);


            SetCurrentTrack(player, index);
            player.StopPlaying(fadeTime);

            if (isCustom)
                CoroutineRunner.Instance.StartCoroutine(PlayCustomTrack(player, track, fadeTime));
            else
                PlayVanilla(player, track, fadeTime);
            
            isSwitchingTracks = false;
            return true;
        }

        public static int GetNextValidTrack(MusicPlaylistPlayer player, int lastTrack)
        {
            MusicPlaylist playlist = player.playlist;
            var scene = player.gameObject.scene.name;
            var allowVanilla = MusicInjector.ShouldIncludeVanilla(scene);

            // Build list of valid tracks
            var validIndices = playlist.tracks
                .Select((track, idx) => new { track, idx })
                .Where(x => File.Exists(x.track.clipName) || allowVanilla)
                .Select(x => x.idx)
                .ToList();

            if (validIndices.Count == 0)
                return -1;

            if (validIndices.Count == 1)
                return validIndices[0];

            var doShuffle =
                lastTrack == -1 || // first time playing
                (lastTrack >= 0 &&
                 lastTrack < playlist.tracks.Count &&
                 playlist.tracks[lastTrack].onTrackEnd == MusicTrack.OnTrackEnd.PlayRandom);

            if (doShuffle)
            {
                return validIndices
                    .Where(i => i != lastTrack)
                    .OrderBy(_ => Rng.Next())
                    .First();
            }

            // Default: pick next in sequence, skipping same track if possible
            var fallback = validIndices.FirstOrDefault(i => i != lastTrack);
            return fallback != -1 ? fallback : validIndices[0];
        }

        private static void SetCurrentTrack(MusicPlaylistPlayer player, int index)
        {
            typeof(MusicPlaylistPlayer).GetField("currentTrack", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(player, index);
        }

        private static int GetCurrentTrack(MusicPlaylistPlayer player)
        {
            return (int)(typeof(MusicPlaylistPlayer)
                .GetField("currentTrack", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(player) ?? -1);
        }

        private static void PlayVanilla(MusicPlaylistPlayer player, MusicTrack track, float fadeTime)
        {
            AudioSource source = player.source;
            source.clip = Resources.Load<AudioClip>(track.clipName);
            source.pitch = track.pitch;
            CoroutineRunner.Instance.StartCoroutine(FadeInAndPlay(source, track.volume, fadeTime));
        }
        
        public static void SkipToNextTrack(MusicPlaylistPlayer player)
        {
            var current = GetCurrentTrack(player);
            var next = GetNextValidTrack(player, current);

            if (next != -1 && next != current)
            {
                TryPlayTrack(player, next, fadeTime: 1f);
            }
        }
        private static IEnumerator PlayCustomTrack(MusicPlaylistPlayer player, MusicTrack track, float fadeTime)
        {
            var url = "file://" + track.clipName.Replace("\\", "/");
            AudioType type = GetAudioType(Path.GetExtension(track.clipName));

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[CustomMusicMod] Failed to load custom track: {track.clipName} | Error: {www.error}");
                    SkipToNextTrack(player);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (!clip || clip.loadState == AudioDataLoadState.Failed)
                {
                    Debug.LogError($"[CustomMusicMod] Invalid or unsupported audio clip: {track.clipName}");
                    SkipToNextTrack(player);
                    yield break;
                }

                AudioSource source = player.source;
                source.clip = clip;
                source.pitch = track.pitch;
                CoroutineRunner.Instance.StartCoroutine(FadeInAndPlay(source, track.volume, fadeTime));
            }
        }

        private static IEnumerator FadeInAndPlay(AudioSource source, float targetVolume, float fadeTime)
        {
            source.volume = 0f;
            source.Play();

            var timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, targetVolume, timer / fadeTime);
                yield return null;
            }

            source.volume = targetVolume;
        }

        private static AudioType GetAudioType(string ext)
        {
            return ext.ToLowerInvariant() switch
            {
                ".mp3" => AudioType.MPEG,
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".aiff" or ".aif" => AudioType.AIFF,
                _ => AudioType.UNKNOWN
            };
        }
    }
}