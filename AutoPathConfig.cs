using System;
using System.Reflection;

namespace AutoPath;

/// <summary>
/// Integrates with the ModConfig mod (soft dependency) to provide
/// an in-game settings UI. Falls back to defaults if ModConfig isn't installed.
/// </summary>
public static class AutoPathConfig
{
    private const string ModId = "AutoPath";
    private const string YoloKey = "yolo_mode";

    public static bool YoloMode { get; private set; }

    private static Type? _apiType;
    private static MethodInfo? _getValueBool;
    private static bool _registered;

    public static void TryRegisterWithModConfig()
    {
        try
        {
            _apiType = FindModConfigApi();
            if (_apiType == null)
                return;

            var configEntryType = _apiType.Assembly.GetType("ModConfig.ConfigEntry");
            var configTypeEnum = _apiType.Assembly.GetType("ModConfig.ConfigType");
            if (configEntryType == null || configTypeEnum == null)
                return;

            // Build ConfigEntry for YOLO toggle
            var entry = Activator.CreateInstance(configEntryType)!;
            SetProp(entry, "Key", YoloKey);
            SetProp(entry, "Label", "YOLO Mode");
            SetProp(entry, "Description", "Auto-advance even when multiple paths are available (random choice)");
            SetProp(entry, "Type", Enum.Parse(configTypeEnum, "Toggle"));
            SetProp(entry, "DefaultValue", false);
            SetProp(entry, "OnChanged", new Action<object>(OnYoloChanged));

            // Create ConfigEntry[] { entry }
            var entriesArray = Array.CreateInstance(configEntryType, 1);
            entriesArray.SetValue(entry, 0);

            // Call ModConfigApi.Register(modId, displayName, entries)
            var registerMethod = _apiType.GetMethod("Register",
                new[] { typeof(string), typeof(string), entriesArray.GetType() });
            registerMethod?.Invoke(null, new object[] { ModId, "AutoPath", entriesArray });

            // Cache GetValue<bool>
            var getValueGeneric = _apiType.GetMethod("GetValue");
            _getValueBool = getValueGeneric?.MakeGenericMethod(typeof(bool));

            _registered = true;
            RefreshValues();
        }
        catch
        {
            // ModConfig not available — use defaults
        }
    }

    public static void RefreshValues()
    {
        if (!_registered || _getValueBool == null)
            return;

        try
        {
            YoloMode = (bool)_getValueBool.Invoke(null, new object[] { ModId, YoloKey })!;
        }
        catch
        {
            YoloMode = false;
        }
    }

    private static void OnYoloChanged(object newValue)
    {
        if (newValue is bool b)
            YoloMode = b;
    }

    private static Type? FindModConfigApi()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "ModConfig")
            {
                return asm.GetType("ModConfig.ModConfigApi");
            }
        }
        return null;
    }

    private static void SetProp(object obj, string name, object value)
    {
        obj.GetType().GetProperty(name)?.SetValue(obj, value);
    }
}
