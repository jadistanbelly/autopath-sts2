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
    private const string DelayKey = "selection_delay";
    private const float DefaultDelay = 0.5f;

    public static bool YoloMode { get; private set; }
    public static float SelectionDelay { get; private set; } = DefaultDelay;

    private static Type? _apiType;
    private static MethodInfo? _getValueBool;
    private static MethodInfo? _getValueDouble;
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

            // YOLO toggle entry
            var yoloEntry = Activator.CreateInstance(configEntryType)!;
            SetProp(yoloEntry, "Key", YoloKey);
            SetProp(yoloEntry, "Label", "YOLO Mode");
            SetProp(yoloEntry, "Description", "Auto-advance even when multiple paths are available (random choice)");
            SetProp(yoloEntry, "Type", Enum.Parse(configTypeEnum, "Toggle"));
            SetProp(yoloEntry, "DefaultValue", false);
            SetProp(yoloEntry, "OnChanged", new Action<object>(OnYoloChanged));

            // Delay slider entry
            var delayEntry = Activator.CreateInstance(configEntryType)!;
            SetProp(delayEntry, "Key", DelayKey);
            SetProp(delayEntry, "Label", "Selection Delay");
            SetProp(delayEntry, "Description", "Seconds to wait before auto-advancing (0.5 – 10)");
            SetProp(delayEntry, "Type", Enum.Parse(configTypeEnum, "Slider"));
            SetProp(delayEntry, "DefaultValue", (double)DefaultDelay);
            SetProp(delayEntry, "Min", 0.5f);
            SetProp(delayEntry, "Max", 10.0f);
            SetProp(delayEntry, "Step", 0.5f);
            SetProp(delayEntry, "Format", "{0:0.0}s");
            SetProp(delayEntry, "OnChanged", new Action<object>(OnDelayChanged));

            // Register both entries
            var entriesArray = Array.CreateInstance(configEntryType, 2);
            entriesArray.SetValue(delayEntry, 0);
            entriesArray.SetValue(yoloEntry, 1);

            var registerMethod = _apiType.GetMethod("Register",
                new[] { typeof(string), typeof(string), entriesArray.GetType() });
            registerMethod?.Invoke(null, new object[] { ModId, "AutoPath", entriesArray });

            // Cache typed GetValue methods
            var getValueGeneric = _apiType.GetMethod("GetValue");
            _getValueBool = getValueGeneric?.MakeGenericMethod(typeof(bool));
            _getValueDouble = getValueGeneric?.MakeGenericMethod(typeof(double));

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
        if (!_registered)
            return;

        try
        {
            if (_getValueBool != null)
                YoloMode = (bool)_getValueBool.Invoke(null, new object[] { ModId, YoloKey })!;
            if (_getValueDouble != null)
                SelectionDelay = (float)(double)_getValueDouble.Invoke(null, new object[] { ModId, DelayKey })!;
        }
        catch
        {
            YoloMode = false;
            SelectionDelay = DefaultDelay;
        }
    }

    private static void OnYoloChanged(object newValue)
    {
        if (newValue is bool b)
            YoloMode = b;
    }

    private static void OnDelayChanged(object newValue)
    {
        if (newValue is double d)
            SelectionDelay = (float)d;
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
