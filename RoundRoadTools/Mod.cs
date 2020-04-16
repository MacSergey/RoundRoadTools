using ColossalFramework;
using ColossalFramework.PlatformServices;
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
    public class UserMod : IUserMod
    {
        public static string SettingsFile => $"{StaticName}{nameof(SettingsFile)}";
        public static string StaticName => nameof(RoundRoadTools);
        public string Name => StaticName;
        public string Description => Name;

        public UserMod()
        {

        }
        private void Init()
        {
            Debug.Log($"{nameof(UserMod)}.{nameof(Init)}");
            try
            {
                if (GameSettings.FindSettingsFileByName(SettingsFile) == null)
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = SettingsFile });
            }
            catch
            {
                Debug.LogError(Localize.InitError);
            }
        }
        public void OnSettingsUI(UIHelperBase helper)
        {
            UI.Option.Init(helper);
        }
    }
}
