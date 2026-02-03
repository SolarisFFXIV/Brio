using System.Collections.Generic;

namespace Brio.Config;

public class InterfaceConfiguration
{
    public OpenBrioBehavior OpenBrioBehavior { get; set; } = OpenBrioBehavior.OnGPoseEnter;
    public bool ShowInGPose { get; set; } = true;
    public bool ShowInCutscene { get; set; } = false;
    public bool ShowWhenUIHidden { get; set; } = false;
    public bool CensorActorNames { get; set; } = true;

    // Transform Movement Speed
    public float DefaultTransformMovementSpeed { get; set; } = 0.01f;

    // Bone Transform Movement Speed
    public float DefaultBoneTransformMovementSpeed { get; set; } = 0.01f;

    // Free Camera Movement Speed
    public float DefaultFreeCameraMovementSpeed { get; set; } = 0.03f;

    // Free Camera Mouse Sensitivity
    public float DefaultFreeCameraMouseSensitivity { get; set; } = 0.1f;

    // Main window dropdown text colors (per widget)
    public Dictionary<string, uint> WidgetDropdownTextColors { get; set; } = new();

    // Entity hierarchy text colors (per container)
    public Dictionary<string, uint> EntityHierarchyTextColors { get; set; } = new();

    // Show color pickers in UI
    public bool ShowColorPickers { get; set; } = true;

    // Widget category visibility in main window
    public Dictionary<string, bool> WidgetVisibility { get; set; } = new()
    {
        { "Appearance", true },
        { "Dynamic Face Control", true },
        { "Posing", true },
        { "Animation Control", true },
        { "Status Effects", true }
    };
}
