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
    public class UserMod : IUserMod
    {
        public string Name => nameof(RoundRoadTools);
        public string Description => Name;

        private string SettingsFile => $"{Name}{nameof(SettingsFile)}";
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
