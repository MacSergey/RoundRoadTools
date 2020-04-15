using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using UnityEngine;

namespace Mod.UI
{
    public class KeyMapping : UICustomControl
    {

        private void Awake()
        {
            Debug.Log($"{nameof(KeyMapping)}.{nameof(Awake)}");
        }
        private void OnEnable() => LocaleManager.eventLocaleChanged += RefreshBindableInputs;
        private void OnDisable() => LocaleManager.eventLocaleChanged -= RefreshBindableInputs;
        private void RefreshBindableInputs()
        {
            Debug.Log($"{nameof(KeyMapping)}.{nameof(RefreshBindableInputs)}");

            foreach (var current in component.GetComponentsInChildren<UIComponent>())
            {
                var uITextComponent = current.Find<UITextComponent>("Binding");
                if (uITextComponent != null)
                {
                    var savedInputKey = uITextComponent.objectUserData as SavedInputKey;
                    if (savedInputKey != null) uITextComponent.text = savedInputKey.ToLocalizedString("KEYNAME");
                }

                var uILabel = current.Find<UILabel>("Name");
                if (uILabel != null) uILabel.text = Locale.Get("KEYMAPPING", uILabel.stringUserData);
            }
        }

        private void AddKeymapping(string label, SavedInputKey savedInputKey)
        {
            var uIPanel = component.AttachUIComponent(UITemplateManager.GetAsGameObject(nameof(KeyMapping))) as UIPanel;
            //if (count++ % 2 == 1) uIPanel.backgroundSprite = null;

            var uILabel = uIPanel.Find<UILabel>("Name");
            var uIButton = uIPanel.Find<UIButton>("Binding");
            //uIButton.eventKeyDown += OnBindingKeyDown;
            //uIButton.eventMouseDown += OnBindingMouseDown;

            uILabel.text = label;
            uIButton.text = savedInputKey.ToLocalizedString("KEYNAME");
            uIButton.objectUserData = savedInputKey;
        }

        //private void OnBindingKeyDown(UIComponent comp, UIKeyEventParameter p)
        //{
        //    if (m_EditingBinding != null && !IsModifierKey(p.keycode))
        //    {
        //        p.Use();
        //        UIView.PopModal();
        //        var inputKey = p.keycode == KeyCode.Escape ? m_EditingBinding.value : SavedInputKey.Encode(p.keycode, p.control, p.shift, p.alt);
        //        if (p.keycode == KeyCode.Backspace) inputKey = SavedInputKey.Empty;
        //        m_EditingBinding.value = inputKey;
        //        var uITextComponent = p.source as UITextComponent;
        //        uITextComponent.text = m_EditingBinding.ToLocalizedString("KEYNAME");
        //        m_EditingBinding = null;
        //        m_EditingBindingCategory = string.Empty;
        //    }
        //}
        private bool IsModifierKey(KeyCode code) => code == KeyCode.LeftControl || code == KeyCode.RightControl || code == KeyCode.LeftShift ||
                   code == KeyCode.RightShift || code == KeyCode.LeftAlt || code == KeyCode.RightAlt;
        private bool IsControlDown() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        private bool IsShiftDown() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private bool IsAltDown() => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private bool IsUnbindableMouseButton(UIMouseButton code) =>  code == UIMouseButton.Left || code == UIMouseButton.Right;
    }
}
