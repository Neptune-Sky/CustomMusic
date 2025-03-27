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

namespace CustomMusic
{
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

        private static int GetNextValidTrack(MusicPlaylistPlayer player, int lastTrack)
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

            switch (validIndices.Count)
            {
                case 0:
                    return -1;
                case 1:
                    return validIndices[0];
            }

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

            // Set internal fade system values
            ReflectionUtils.SetPrivateField(player, "fadeTime", fadeTime);
            ReflectionUtils.SetPrivateField(player, "targetFadeVolume", 1f);
            ReflectionUtils.SetPrivateField(player, "currentFadeVolume", 0f); // start silent and let vanilla fade it in

            // Call UpdateVolume once immediately to initialize volume state
            var updateVolume = typeof(MusicPlaylistPlayer)
                .GetMethod("UpdateVolume", BindingFlags.NonPublic | BindingFlags.Instance);
            updateVolume?.Invoke(player, null);

            source.Play();
        }

        private static void SkipToNextTrack(MusicPlaylistPlayer player)
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

            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, type);
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
            ReflectionUtils.SetPrivateField(player, "fadeTime", fadeTime);
            ReflectionUtils.SetPrivateField(player, "targetFadeVolume", 1f);
            ReflectionUtils.SetPrivateField(player, "currentFadeVolume", 0f); // or preload to avoid ramp

            var updateVolume = typeof(MusicPlaylistPlayer).GetMethod("UpdateVolume", BindingFlags.NonPublic | BindingFlags.Instance);
            updateVolume?.Invoke(player, null);

            source.Play();
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