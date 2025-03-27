using System.Collections.Generic;
using System.IO;
using System.Linq;
using SFS.Audio;

namespace CustomMusic
{
    public static class VanillaPlaylistCache
    {
        private static readonly Dictionary<MusicPlaylist, List<MusicTrack>> cache = new();

        public static void CacheIfNeeded(MusicPlaylist playlist)
        {
            if (!cache.ContainsKey(playlist))
            {
                // Only store non-custom tracks
                var vanillaTracks = playlist.tracks
                    .Where(t => !File.Exists(t.clipName))
                    .Select(CloneTrack)
                    .ToList();

                cache[playlist] = vanillaTracks;
            }
        }

        public static List<MusicTrack> GetCachedVanilla(MusicPlaylist playlist)
        {
            return cache.TryGetValue(playlist, out var tracks) ? tracks : new List<MusicTrack>();
        }

        private static MusicTrack CloneTrack(MusicTrack track)
        {
            return new MusicTrack
            {
                clipName = track.clipName,
                pitch = track.pitch,
                volume = track.volume,
                onTrackEnd = track.onTrackEnd
            };
        }
    }

}