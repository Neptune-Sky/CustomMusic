using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using JetBrains.Annotations;
using ModLoader;
using ModLoader.Helpers;
using SFS.IO;
using UITools;
using UnityEngine;

namespace CustomMusic
{
    [UsedImplicitly]
    public class Main : Mod, IUpdatable
    {
        public override string ModNameID => "CustomMusic";
        public override string DisplayName => "Custom Music";
        public override string Author => "NeptuneSky";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v2.0";
        public override string Description => "Simple mod that lets you import custom music.";

        public Dictionary<string, FilePath> UpdatableFiles => new()
        {
            {
                "https://github.com/Neptune-Sky/SFSCustomMusic/releases/latest/download/CustomMusic.dll",
                new FolderPath(ModFolder).ExtendToFile("CustomMusic.dll")
            }
        };


        public static GameObject loaderObject;

        private static Harmony patcher;

        public static FolderPath modFolder;


        private static readonly string[] SceneFolders = new[]
        {
            "Home_PC",
            "Build_PC",
            "World_PC"
        };

        private void EnsureDirectoriesExist()
        {
            var musicDir = Path.Combine(ModFolder, "Music");
            try
            {
                if (!Directory.Exists(musicDir))
                {
                    Directory.CreateDirectory(musicDir);
                }

                foreach (var scene in SceneFolders)
                {
                    var subfolder = Path.Combine(musicDir, scene);
                    if (!Directory.Exists(subfolder))
                    {
                        Directory.CreateDirectory(subfolder);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CustomMusicMod] Failed to create music directories: {ex}");
            }
        }

        public override void Early_Load()
        {
            modFolder = new FolderPath(ModFolder);
            EnsureDirectoriesExist();

            Config.Load();
            
            patcher = new Harmony("mods.NeptuneSky.CustomMusic");
            patcher.PatchAll();
            CoroutineRunner.Create();
            
            SceneHelper.OnSceneLoaded += (scene) =>
            {
                CoroutineRunner.Instance.StartCoroutine(MusicInjector.InjectAfterSceneLoad(scene.name));
            };
        }

        public override void Load()
        {
            
        }
    }
}
