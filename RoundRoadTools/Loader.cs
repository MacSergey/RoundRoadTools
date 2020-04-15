using ColossalFramework;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using Mod.UI.Buttons;
using Mod.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Mod
{
    public class Loader : LoadingExtensionBase
    {
        public static RoundRoadTools ToolInstance { get; set; }
        public static LoadMode LoadMode { get; set; }
        public static string AtlasName => nameof(RoundRoadTools);
        private bool IsAtlasLoaded => SpriteUtility.GetAtlas(AtlasName) is UITextureAtlas;
        private bool IsRunningGui { get; set; } = false;

        public override void OnCreated(ILoading loading) => base.OnCreated(loading);

        public override void OnLevelLoaded(LoadMode mode)
        {
            Debug.Log($"{nameof(Loader)}.{nameof(OnLevelLoaded)}");
            base.OnLevelLoaded(mode);

            LoadMode = mode;
            switch (mode)
            {
                case LoadMode.LoadGame:
                case LoadMode.NewGame:
                case LoadMode.NewMap:
                case LoadMode.LoadMap:
                case LoadMode.NewAsset:
                case LoadMode.LoadAsset:
                    SetupGui();
                    SetupTools();
                    break;
            }
        }

        public static void SetupTools()
        {
            Debug.Log($"{nameof(Loader)}.{nameof(SetupTools)}");

            if (!(ToolInstance is RoundRoadTools instance))
            {
                ToolController toolController = GameObject.FindObjectOfType<ToolController>();
                instance = toolController.gameObject.AddComponent<RoundRoadTools>();
                ToolInstance = instance;
                instance.enabled = false;
            }

            instance.Init();
        }
        public void SetupGui()
        {
            LoadSprites();
            if (IsAtlasLoaded)
            {
                SetupButton<RoundButton>();
                IsRunningGui = true;
            }
        }
        public void SetupButton<T>() where T : UIButton
        {
            if (!(UIView.GetAView().GetComponent<T>() is T))
                UIView.GetAView().AddUIComponent(typeof(T));
        }

        private void LoadSprites()
        {
            if (IsAtlasLoaded)
                return;

            var modPath = PluginManager.instance.FindPluginInfo(Assembly.GetExecutingAssembly()).modPath;
            if (SpriteUtility.InitialiseAtlas(Path.Combine(modPath, "Resources/Icon.png"), AtlasName))
            {
                SpriteUtility.AddSpriteToAtlas(new Rect(new Vector2(66, 2), new Vector2(30, 30)), RoundButton.NormalBgSprite, AtlasName);
                SpriteUtility.AddSpriteToAtlas(new Rect(new Vector2(98, 2), new Vector2(30, 30)), RoundButton.HoveredBgSprite, AtlasName);
            }
        }
    }
}
