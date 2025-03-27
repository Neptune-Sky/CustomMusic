using System;
using SFS.IO;
using SFS.UI.ModGUI;
using SFS.Variables;
using TMPro;
using UITools;
using UnityEngine;
using static SFS.UI.ModGUI.Builder;
using Type = SFS.UI.ModGUI.Type;

namespace CustomMusic
{
    public class Config : ModSettings<Config.SettingsData>
    {
        protected override FilePath SettingsFile => Main.modFolder.ExtendToFile("Config.txt");

        private static Config main;

        public static void Load()
        {
            main = new Config();
            main.Initialize();
            ConfigurationMenu.Add("Custom Music", new (string, Func<Transform, GameObject>)[]
            {
                ("Config", transform1 => MenuItems(transform1, ConfigurationMenu.ContentSize))
            });
        }

        public class SettingsData
        {
            public Bool_Local homeMenu = new();
            public Bool_Local buildScene = new();
            public Bool_Local worldScene = new();
        }

        private static GameObject MenuItems(Transform parent, Vector2Int size)
        {
            Box box = CreateBox(parent, size.x, size.y);
            box.CreateLayoutGroup(Type.Vertical, TextAnchor.UpperCenter, 35, new RectOffset(15, 15, 15, 15));
            int width = size.x - 60;
            CreateLabel(box, size.x, 50, text: "Custom Music");

            Container scale = CreateContainer(box);
            scale.CreateLayoutGroup(Type.Horizontal, spacing: 0);
            CreateLabel(box, width, 32, text: "Allow vanilla music in:");
            CreateToggleWithLabel(box, width, 32, () => settings.homeMenu, () =>
                {
                    settings.homeMenu.Value = !settings.homeMenu.Value;
                    MusicInjector.OnSceneToggleChanged("Home_PC");
                },
                labelText: "Home Menu");
            CreateToggleWithLabel(box, width, 32, () => settings.buildScene, () =>
                {
                    settings.buildScene.Value = !settings.buildScene.Value;
                    MusicInjector.OnSceneToggleChanged("Build_PC");
                },
                labelText: "Build Scene");
            CreateToggleWithLabel(box, width, 32, () => settings.worldScene, () =>
                {
                    settings.worldScene.Value = !settings.worldScene.Value;
                    MusicInjector.OnSceneToggleChanged("World_PC");
                },
                labelText: "World Scene");
            return box.gameObject;
        }

        protected override void RegisterOnVariableChange(Action onChange)
        {
            Application.quitting += onChange;
        }
    }
}
