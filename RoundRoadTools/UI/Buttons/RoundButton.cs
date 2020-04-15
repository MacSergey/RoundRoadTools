using ColossalFramework.UI;
using Mod.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Mod.UI.Buttons
{
    public class RoundButton : UIButton
    {
        public static string NormalBgSprite { get; } = nameof(RoundButton);
        public static string HoveredBgSprite { get; } = $"{nameof(RoundButton)}Hovered";
        public override void Start()
        {
            name = nameof(RoundButton);
            normalBgSprite = NormalBgSprite;
            hoveredBgSprite = HoveredBgSprite;

            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            var pos = new Vector2((145f), (resolution.y * 4 / 5) - 50);
            Rect rect = new Rect(pos.x, pos.y, 30, 30);
            SpriteUtility.ClampRectToScreen(ref rect, resolution);

            absolutePosition = rect.position;
            size = new Vector2(30f, 30f);

            atlas = SpriteUtility.GetAtlas(Loader.AtlasName);
            zOrder = 11;

            eventClick += RoundButtonClick;
        }

        private void RoundButtonClick(UIComponent component, UIMouseEventParameter eventParam)
        {
            var instance = Loader.ToolInstance;

            if (!instance.enabled && ToolsModifierControl.GetCurrentTool<ToolBase>() is NetTool netTool)
                instance.NetInfo = netTool.m_prefab;

            ToolsModifierControl.SetTool<DefaultTool>();
            instance.enabled = !instance.enabled;
        }
        public void OnGUI()
        {
            var isNetTool = ToolsModifierControl.GetCurrentTool<ToolBase>() is NetTool;
            if (!isVisible && isNetTool)
            {
                Show();
                Debug.LogWarning($"{nameof(RoundButton)} - Show");
            }
            else if (isVisible && !isNetTool)
            {
                Hide();
                Debug.LogWarning($"{nameof(RoundButton)} - Hide");
            }
        }
    }
}
