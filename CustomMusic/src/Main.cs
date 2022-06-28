using HarmonyLib;
using System;
using ModLoader;
using SFS.Audio;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CustomMusic
{
    public class Main : Mod
    {
        public Main() : base(
           "CustomMusic", // Mod id
           "Custom Music", // Mod Name
           "ASoD", // Mod Author
           "0.5.7", // Loader
           "v1.0.0", // Mod version
           "Simple mod that lets you import custom music."
           )
        { }

        public static GameObject loaderObject;

        public static Harmony patcher;

        public static string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Custom Music";

        public static string configPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\Config.txt";

        public static JObject defaultConfig = JObject.Parse("{ " + 
            "allowDefaultBuildMusic: true," +
            "allowDefaultWorldMusic: true" +
            "}");

        public static JObject settings;

        public static MusicPlaylistPlayer player;

        public static string worldDir = baseDir + "\\World";

        public static string buildDir = baseDir + "\\Build";

        public static DirectoryInfo worldDirInfo;

        public static DirectoryInfo buildDirInfo;

        public static List<MusicTrack> spaceTracks = new List<MusicTrack>();

        public static List<MusicTrack> buildTracks = new List<MusicTrack>();

        public static FileInfo[] worldInfo;

        public static FileInfo[] buildInfo;

        public static string currentScene = "Base_PC";

        public override void Early_Load()
        {
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }
            if (!Directory.Exists(worldDir))
            {
                Directory.CreateDirectory(worldDir);
            }
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, defaultConfig.ToString());
            }

            try
            {
                settings = JObject.Parse(File.ReadAllText(configPath));
                bool defaultBuild = (bool)settings["allowDefaultBuildMusic"];
                bool defaultWorld = (bool)settings["allowDefaultWorldMusic"];
            }
            catch (Exception)
            {
                File.WriteAllText(configPath, defaultConfig.ToString());
                Debug.Log("Config file was of an invalid format, and was reset to defaults.");
                settings = defaultConfig;
            }

            worldDirInfo = new DirectoryInfo(worldDir);
            worldInfo = worldDirInfo.GetFiles("*.ogg");

            buildDirInfo = new DirectoryInfo(buildDir);
            buildInfo = buildDirInfo.GetFiles("*.ogg");

            loaderObject = new GameObject();
            loaderObject.AddComponent<Loader>();
            loaderObject.AddComponent<Loader2>();

            loaderObject.SetActive(true);

            patcher = new Harmony("mods.ASoD.CustomMusic");
            patcher.PatchAll();

            return;
        }

        public override void Load()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public override void Unload()
        {
            throw new NotImplementedException();
        }

        public void OnSceneLoaded (Scene scene, LoadSceneMode load)
        {
            currentScene = scene.name;
        }


    }

    [HarmonyPatch(typeof(MusicPlaylistPlayer), "StartPlaying")]
    static class AddTracks
    {
        static void Prefix(ref MusicPlaylistPlayer __instance)
        {
            Main.player = __instance;
            switch (Main.currentScene)
            {
                case "Base_PC":
                case "World_PC":
                    if ((bool)Main.settings["allowDefaultWorldMusic"])
                    {
                        __instance.playlist.tracks.AddRange(Main.spaceTracks);
                    }
                    else
                    {
                        __instance.playlist.tracks = (Main.spaceTracks);
                    }

                    break;
                case "Build_PC":
                    if ((bool)Main.settings["allowDefaultBuildMusic"])
                    {
                        __instance.playlist.tracks.AddRange(Main.buildTracks);
                    }
                    else
                    {
                        __instance.playlist.tracks = (Main.buildTracks);
                    }
                    break;
            }


        }
    }
    [HarmonyPatch(typeof(MusicPlaylistPlayer), "PlayTrack")]
    static class MusicCheck
    {
        static bool Prefix(ref MusicPlaylistPlayer __instance)
        {
            if ((__instance.playlist.tracks).Count == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
    [HarmonyPatch(typeof(MusicPlaylistPlayer), "Update")]
    static class StartMusic
    {
        static void Prefix(ref MusicPlaylistPlayer __instance)
        {
            if (Main.spaceTracks != new List<MusicTrack>() && (int)Traverse.Create(__instance).Field("currentTrack").GetValue() == -1 && Main.currentScene == "Base_PC")
            {
                __instance.StartPlaying((float)Traverse.Create(__instance).Field("fadeTime").GetValue());
            }
        }
    }

    class GetAudio
    {
        public static IEnumerator LoadAudioClipsFromFiles(FileInfo[] files, AudioType type, bool isBuild)
        {
            foreach (var file in files)
            {

                var path = Path.Combine("file://", file.FullName);
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, type))
                {
                    yield return www.SendWebRequest();

                    try
                    {
                        if (www.result == UnityWebRequest.Result.ConnectionError)
                        {
                            Debug.Log(www.result);
                        }
                        else
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                            clip.name = Path.GetFileNameWithoutExtension(file.Name);
                            // Do whatevs you want
                            Debug.Log("Custom Music: Loading " + clip.name + "...");
                            MusicTrack music = new MusicTrack();
                            music.clip = clip;
                            if (!isBuild)
                            {
                                Main.spaceTracks.Add(music);
                            }
                            else
                            {
                                Main.buildTracks.Add(music);
                            }



                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e);
                    }
                }
            }
            yield break;
        }
    }

    public class Loader : MonoBehaviour
    {
        public IEnumerator Start()
        {
            yield return StartCoroutine(GetAudio.LoadAudioClipsFromFiles(Main.worldInfo, AudioType.OGGVORBIS, false));
            yield break; 
        }
    }
    public class Loader2 : MonoBehaviour
    {
        public IEnumerator Start()
        {
            yield return StartCoroutine(GetAudio.LoadAudioClipsFromFiles(Main.buildInfo, AudioType.OGGVORBIS, true));
            yield break; 
        }
    }
}
