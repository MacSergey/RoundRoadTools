using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using System.Reflection;
using UnityEngine;

namespace Mod.UI
{
    public class KeyMapping : UICustomControl
    {
        public static string BindingTemplate => $"KeyBindingTemplate";

        public static SavedInputKey RadiusPlus { get; } = new SavedInputKey(nameof(RadiusPlus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Equals, false, false, false), true);
        public static SavedInputKey RadiusMinus { get; } = new SavedInputKey(nameof(RadiusMinus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Minus, false, false, false), true);
        public static SavedInputKey SegmentPlus { get; } = new SavedInputKey(nameof(SegmentPlus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Equals, false, true, false), true);
        public static SavedInputKey SegmentMinus { get; } = new SavedInputKey(nameof(SegmentMinus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Minus, false, true, false), true);
        public static SavedInputKey StartShiftPlus { get; } = new SavedInputKey(nameof(StartShiftPlus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Equals, true, true, false), true);
        public static SavedInputKey StartShiftMinus { get; } = new SavedInputKey(nameof(StartShiftMinus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Minus, true, true, false), true);
        public static SavedInputKey EndShiftPlus { get; } = new SavedInputKey(nameof(EndShiftPlus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Equals, false, true, true), true);
        public static SavedInputKey EndShiftMinus { get; } = new SavedInputKey(nameof(EndShiftMinus), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.Minus, false, true, true), true);
        public static SavedInputKey SandGlass { get; } = new SavedInputKey(nameof(SandGlass), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.K, false, false, false), true);
        public static SavedInputKey ShowShift { get; } = new SavedInputKey(nameof(ShowShift), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.L, false, false, false), true);
        public static SavedInputKey Build { get; } = new SavedInputKey(nameof(Build), UserMod.SettingsFile, SavedInputKey.Encode(KeyCode.J, false, false, false), true);

        private SavedInputKey EditingBinding { get; set; }

        private void Awake()
        {
            Debug.Log($"{nameof(KeyMapping)}.{nameof(Awake)}");

            AddKeymapping(Localize.HotkeyRadiusPlus, RadiusPlus);
            AddKeymapping(Localize.HotkeyRadiusMinus, RadiusMinus);

            AddKeymapping(Localize.HotkeySegmentPlus, SegmentPlus);
            AddKeymapping(Localize.HotkeySegmentMinus, SegmentMinus);

            AddKeymapping(Localize.HotkeyStartShiftPlus, StartShiftPlus);
            AddKeymapping(Localize.HotkeyStartShiftMinus, StartShiftMinus);

            AddKeymapping(Localize.HotkeyEndShiftPlus, EndShiftPlus);
            AddKeymapping(Localize.HotkeyEndShiftMinus, EndShiftMinus);

            AddKeymapping(Localize.HotkeySandGlass, SandGlass);

            AddKeymapping(Localize.HotkeyShowShift, ShowShift);
        }

        private void AddKeymapping(string label, SavedInputKey savedInputKey)
        {
            var uIPanel = component.AttachUIComponent(UITemplateManager.GetAsGameObject(BindingTemplate)) as UIPanel;

            var uILabel = uIPanel.Find<UILabel>("Name");
            var uIButton = uIPanel.Find<UIButton>("Binding");
            uIButton.eventKeyDown += OnBindingKeyDown;
            uIButton.eventMouseDown += OnBindingMouseDown;

            uILabel.text = label;
            uIButton.text = savedInputKey.ToLocalizedString("KEYNAME");
            uIButton.objectUserData = savedInputKey;
        }

        private void OnBindingKeyDown(UIComponent comp, UIKeyEventParameter p)
        {
            if (EditingBinding == null || IsModifierKey(p.keycode))
                return;

            p.Use();
            UIView.PopModal();

            switch (p.keycode)
            {
                case KeyCode.Escape:
                    break;
                case KeyCode.Backspace:
                    EditingBinding.value = SavedInputKey.Empty;
                    break;
                default:
                    EditingBinding.value = SavedInputKey.Encode(p.keycode, p.control, p.shift, p.alt);
                    break;
            }

            var uITextComponent = p.source as UITextComponent;
            uITextComponent.text = EditingBinding.ToLocalizedString("KEYNAME");

            EditingBinding = null;
        }
        private void OnBindingMouseDown(UIComponent comp, UIMouseEventParameter p)
        {
            if (EditingBinding == null)
            {
                p.Use();
                EditingBinding = (SavedInputKey)p.source.objectUserData;

                var uIButton = p.source as UIButton;
                uIButton.buttonsMask = UIMouseButton.Left | UIMouseButton.Right | UIMouseButton.Middle | UIMouseButton.Special0 | UIMouseButton.Special1 | UIMouseButton.Special2 | UIMouseButton.Special3;
                uIButton.text = "Press any key";

                p.source.Focus();
                UIView.PushModal(p.source);
            }
            else if (!IsUnbindableMouseButton(p.buttons))
            {
                p.Use();
                UIView.PopModal();

                var inputKey = SavedInputKey.Encode(ButtonToKeycode(p.buttons), IsControlDown(), IsShiftDown(), IsAltDown());
                EditingBinding.value = inputKey;

                var uIButton = p.source as UIButton;
                uIButton.text = EditingBinding.ToLocalizedString("KEYNAME");
                uIButton.buttonsMask = UIMouseButton.Left;

                EditingBinding = null;
            }
        }

        private void OnEnable() => LocaleManager.eventLocaleChanged += RefreshBindableInputs;
        private void OnDisable() => LocaleManager.eventLocaleChanged -= RefreshBindableInputs;

        private void RefreshBindableInputs()
        {
            Debug.Log($"{nameof(KeyMapping)}.{nameof(RefreshBindableInputs)}");

            foreach (var current in component.GetComponentsInChildren<UIComponent>())
            {
                if (current.Find<UITextComponent>("Binding") is UITextComponent uITextComponent)
                {
                    if (uITextComponent.objectUserData is SavedInputKey savedInputKey)
                        uITextComponent.text = savedInputKey.ToLocalizedString("KEYNAME");
                }

                if (current.Find<UILabel>("Name") is UILabel uILabel)
                    uILabel.text = Locale.Get("KEYMAPPING", uILabel.stringUserData);
            }
        }
        internal InputKey GetDefaultEntry(string entryName) => typeof(DefaultSettings).GetField(entryName, BindingFlags.Static | BindingFlags.Public) is FieldInfo field && field.GetValue(null) is InputKey key ? key : (InputKey)0;


        private bool IsModifierKey(KeyCode code) => code == KeyCode.LeftControl || code == KeyCode.RightControl || code == KeyCode.LeftShift ||
                   code == KeyCode.RightShift || code == KeyCode.LeftAlt || code == KeyCode.RightAlt;
        private bool IsControlDown() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        private bool IsShiftDown() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private bool IsAltDown() => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        private bool IsUnbindableMouseButton(UIMouseButton code) => code == UIMouseButton.Left || code == UIMouseButton.Right;
        private KeyCode ButtonToKeycode(UIMouseButton button)
        {
            switch (button)
            {
                case UIMouseButton.Left: return KeyCode.Mouse0;
                case UIMouseButton.Right: return KeyCode.Mouse1;
                case UIMouseButton.Middle: return KeyCode.Mouse2;
                case UIMouseButton.Special0: return KeyCode.Mouse3;
                case UIMouseButton.Special1: return KeyCode.Mouse4;
                case UIMouseButton.Special2: return KeyCode.Mouse5;
                case UIMouseButton.Special3: return KeyCode.Mouse6;
                default: return KeyCode.None;
            }
        }
    }
}
