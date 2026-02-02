using Brio.Capabilities.Camera;
using Brio.Entities.Core;
using Brio.Game.Camera;
using Brio.Game.Input;
using Brio.UI.Controls.Editors;
using Brio.UI.Controls.Stateless;
using Brio.UI.Theming;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Brio.Entities.Camera;

public class CameraContainerEntity(IServiceProvider provider) : Entity("cameras", provider)
{
    private readonly VirtualCameraManager _virtualCameraManager = provider.GetRequiredService<VirtualCameraManager>();
    private readonly GameInputService _gameInputService = provider.GetRequiredService<GameInputService>();

    public override string FriendlyName => "Cameras";

    public override FontAwesomeIcon Icon => FontAwesomeIcon.Camera;

    public override int ContextButtonCount => 3;

    public override EntityFlags Flags => EntityFlags.DefaultOpen | EntityFlags.HasContextButton;

    public override void DrawContextButton()
    {
        using(ImRaii.PushColor(ImGuiCol.Button, ThemeManager.CurrentTheme.Accent.AccentColor))
        {
            bool movementLocked = _virtualCameraManager.DisableAllCameraMovement;
            string lockToolTip = movementLocked ? "Enable Camera Movement" : "Disable Camera Movement";
            var lockIcon = movementLocked ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
            if(movementLocked)
                ImGui.PushStyleColor(ImGuiCol.Button, ThemeManager.CurrentTheme.Accent.AccentColor);

            if(ImBrio.FontIconButtonRight($"###{Id}_cameras_movement_lock", lockIcon, 3f, lockToolTip, bordered: false))
            {
                _virtualCameraManager.DisableAllCameraMovement = !movementLocked;
            }

            if(movementLocked)
                ImGui.PopStyleColor();

            ImGui.SameLine();
            string toolTip = "New Camera";
            if(ImBrio.FontIconButtonRight($"###{Id}_cameras_contextButton", FontAwesomeIcon.Plus, 2f, toolTip, bordered: false))
            {
                ImGui.OpenPopup("DrawSpawnMenuPopup");
            }
            CameraEditor.DrawSpawnMenu(_virtualCameraManager);
        }
    }

    public override void OnAttached()
    {
        AddCapability(ActivatorUtilities.CreateInstance<CameraContainerCapability>(_serviceProvider, this));
    }

    public override void OnSelected()
    {
        _gameInputService.AllowEscape = true;
        base.OnSelected();
    }

    public override void OnChildAttached() => SortChildren();
    public override void OnChildDetached() => SortChildren();

    private void SortChildren() =>
        _children.Sort(static (a, b) => string.Compare(a.Id.Unique, b.Id.Unique, StringComparison.Ordinal));

}
