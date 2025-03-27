using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using ModLoader;
using ModLoader.Helpers;
using SFS.Audio;
using SFS.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomMusic
{
    public class Main : Mod
    {
        public override string ModNameID => "CustomMusic";
        public override string DisplayName => "Custom Music";
        public override string Author => "NeptuneSky";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.3";
        public override string Description => "Simple mod that lets you import custom music.";


        public static GameObject loaderObject;

        public static Harmony patcher;

        public static FolderPath modFolder;

        public override void Early_Load()
        {
            modFolder = new FolderPath(ModFolder);
            var musicFolder = modFolder.CloneAndExtend("Music");
            musicFolder.CloneAndExtend("Home_PC");
            musicFolder.CloneAndExtend("Build_PC");
            musicFolder.CloneAndExtend("World_PC");

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
