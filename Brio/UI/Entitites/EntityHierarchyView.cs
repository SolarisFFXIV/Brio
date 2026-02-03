using Brio.Capabilities.Posing;
using Brio.Config;
using Brio.Entities;
using Brio.Entities.Actor;
using Brio.Entities.Core;
using Brio.Entities.World;
using Brio.Game.GPose;
using Brio.Game.Posing;
using Brio.Game.World;
using Brio.Input;
using Brio.Services;
using Brio.UI.Controls.Stateless;
using Brio.UI.Theming;
using Brio.UI.Widgets.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Brio.UI.Entitites;

public class EntityHierarchyView(EntityManager entityManager, GPoseService gPoseService, HistoryService groupedUndoService, ConfigurationService configurationService, LightingService lightingService)
{
    private float buttonWidth => ImGui.GetWindowContentRegionMax().X;
    private readonly float offsetWidth = 18f;

    private static readonly HashSet<string> ColorableContainerIds = new()
    {
        "actorContainer",
        "cameras",
        "environment"
    };

    private EntityId? _lastSelectedId;

    public void Draw(Entity root)
    {
        if(root.IsVisible is false)
            return;

        var selectedEntityId = entityManager.SelectedEntityId;

        if(_lastSelectedId != null && selectedEntityId != null && !_lastSelectedId.Equals(selectedEntityId))
        {
            // The change must have come from outside of this control
            _lastSelectedId = selectedEntityId;
        }

        if(ImGui.IsWindowHovered())
        {
            if(InputManagerService.ActionKeysPressed(InputAction.Interface_SelectAllActors))
            {
                entityManager.ClearSelectedEntities();
                foreach(var e in entityManager.TryGetAllActors())
                {
                    entityManager.AddSelectedEntity(e.Id);
                }
            }
        }
        
        using(ImRaii.PushId($"entity_hierarchy_{root.Id}"))
        {
            foreach(var item in root.Children)
            {
                var disable = gPoseService.IsGPosing == false && item.Flags.HasFlag(EntityFlags.AllowOutsideGpose) == false;
                try
                {
                    using(ImRaii.Disabled(disable))
                        DrawEntity(item, selectedEntityId, null);
                }
                catch(System.Exception ex)
                {
                    Brio.Log.Error($"Error drawing entity {item.FriendlyName} ({item.Id}): {ex}");
                }
            }
        }
    }

    private void DrawEntity(Entity entity, EntityId? selectedEntityId, uint? inheritedColor, float lastOffset = 0)
    {
        bool isSelected = false;
        bool hasChildren = false;
        bool hasOffset = false;

        bool isMutiSelected = false;

        if(lastOffset > 0)
            hasOffset = true;
        if(entity.Children.Count > 0)
            hasChildren = true;
        if(selectedEntityId != null && entity.Id.Equals(selectedEntityId))
            isSelected = true;

        var currentSelected = entityManager.SelectedEntity;
        var currentSupportsMultiSelect = currentSelected?.Flags.HasFlag(EntityFlags.AllowMultiSelect) ?? false;

        var entityAllowsMultiSelect = entity.Flags.HasFlag(EntityFlags.AllowMultiSelect);

        if(entityManager.SelectedEntityIds.Contains(entity.Id) && entityAllowsMultiSelect)
            isMutiSelected = true;

        var config = ConfigurationService.Instance?.Configuration;
        uint? effectiveColor = inheritedColor;

        if(IsColorableContainer(entity) && config?.Interface.EntityHierarchyTextColors != null
            && config.Interface.EntityHierarchyTextColors.TryGetValue(entity.Id.Unique, out var storedColor))
        {
            effectiveColor = storedColor;
        }

        using(ImRaii.PushColor(ImGuiCol.ButtonActive, 0))
        {
            using(ImRaii.PushColor(ImGuiCol.Button, 0))
            {
                var invsButtonPos = ImGui.GetCursorPos();

                float width = buttonWidth;

                if(entity.ContextButtonCount >= 1)
                    width -= (33 * ImGuiHelpers.GlobalScale * entity.ContextButtonCount);
                else
                    width -= 5;

                if(ImGui.Button($"###{entity.Id}_invs_button", new(width, 24 * ImGuiHelpers.GlobalScale)))
                {
                    var io = ImGui.GetIO();

                    // Ctrl+Click toggles selection
                    if(InputManagerService.ActionKeysPressed(InputAction.Brio_Ctrl) && entityAllowsMultiSelect)
                    {
                        if(!currentSupportsMultiSelect && currentSelected != null)
                        {
                            groupedUndoService.Clear();
                            Select(entity);
                        }
                        else
                        {
                            if(entityManager.SelectedEntityIds.Contains(entity.Id))
                                entityManager.RemoveSelectedEntity(entity.Id);
                            else
                                entityManager.AddSelectedEntity(entity.Id);
                        }
                    }
                    else
                    {
                        groupedUndoService.Clear();

                        Select(entity);
                    }
                }

                if(ImGui.IsItemHovered())
                {
                    if(entity.Flags.HasFlag(EntityFlags.AllowDoubleClick) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        entity.OnDoubleClick();
                    }
                }
                if(ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    ImGui.OpenPopup($"context_popup{entity.Id}");
                }

                ImGui.SetCursorPos(invsButtonPos);
            }

            if(hasOffset)
            {
                var curPos = ImGui.GetCursorPos();

                ImGui.SetCursorPos(new Vector2(curPos.X + (lastOffset), curPos.Y));
                lastOffset += offsetWidth;
            }

            using(ImRaii.PushColor(ImGuiCol.Button, ThemeManager.CurrentTheme.Accent.AccentColor, isSelected || isMutiSelected))
            {
                using(ImRaii.Disabled(true))
                {
                    ImGui.Button($"###tab_{entity.Id}", new Vector2(8 * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale));
                }
            }
        }

        DrawNode(entity, effectiveColor);

        if(IsColorableContainer(entity) && config?.Interface.ShowColorPickers == true)
            DrawEntityColorPicker(entity, config, effectiveColor);

        if(entity.Flags.HasFlag(EntityFlags.HasContextButton))
        {
            ImGui.SameLine();

            entity.DrawContextButton();
        }

        using(var popup = ImRaii.Popup($"context_popup{entity.Id}"))
        {
            if(popup.Success)
            {
                foreach(var v in entity.Capabilities)
                {
                    if(v.Widget is not null && v.Widget.Flags.HasFlag(WidgetFlags.DrawPopup))
                    {
                        v.Widget.DrawPopup();
                    }
                }
            }
        }

        if(hasChildren)
        {
            foreach(var child in entity.Children)
            {
                DrawEntity(child, selectedEntityId, effectiveColor, lastOffset == 0 ? 3 : lastOffset);
            }
        }
    }

    private static void DrawNode(Entity entity, uint? textColor)
    {
        var nodeStartPos = ImGui.GetCursorPos();

        ImGui.SameLine();
        ImGui.Text(" ");

        ImGui.SameLine();
        if(textColor.HasValue)
        {
            using(ImRaii.PushColor(ImGuiCol.Text, textColor.Value))
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.Text($"{entity.Icon.ToIconString()}");
            }
        }
        else
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGui.Text($"{entity.Icon.ToIconString()}");
            }
        }

        ImGui.SameLine();
        if(textColor.HasValue)
        {
            using(ImRaii.PushColor(ImGuiCol.Text, textColor.Value))
                ImGui.Text(entity.FriendlyName);
        }
        else
        {
            ImGui.Text(entity.FriendlyName);
        }

        ImGui.SetCursorPos(nodeStartPos);
    }

    private static void DrawEntityColorPicker(Entity entity, Configuration? config, uint? currentColor)
    {
        if(config == null)
            return;

        var style = ImGui.GetStyle();
        bool hasScrollbar = ImGui.GetScrollMaxY() > 0;
        float scrollbarWidth = hasScrollbar ? style.ScrollbarSize : 0;

        float unit = ImBrio.ScrollbarSize.X + (style.FramePadding.X * 2);
        var cursorPos = ImGui.GetWindowSize().X - scrollbarWidth - unit;

        ImGui.SameLine();
        ImGui.SetCursorPosX(cursorPos);

        uint baseColor = currentColor ?? ImGui.GetColorU32(ImGuiCol.Text);
        Vector4 color = ImGui.ColorConvertU32ToFloat4(baseColor);

        ImGui.SetNextItemWidth(18 * ImGuiHelpers.GlobalScale);
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha;

        if(ImGui.ColorEdit4($"##{entity.Id.Unique}_hierarchy_text_color", ref color, flags))
        {
            config.Interface.EntityHierarchyTextColors[entity.Id.Unique] = ImGui.ColorConvertFloat4ToU32(color);
            ConfigurationService.Instance?.ApplyChange();
        }
    }

    private static bool IsColorableContainer(Entity entity) => ColorableContainerIds.Contains(entity.Id.Unique);

    private void Select(Entity entity)
    {
        _lastSelectedId = entity.Id;
        entityManager.SetSelectedEntity(entity);

        // Auto-select Model Transform bone if the setting is enabled and we're selecting an actor
        if(configurationService.Configuration.Posing.AutoSelectModelTransformOnActorSelection)
        {
            if(entity is ActorEntity actorEntity)
            {
                if(actorEntity.TryGetCapability<PosingCapability>(out var posingCapability))
                {
                    posingCapability.ClearSelection();
                    posingCapability.Selected = PosingSelectionType.ModelTransform;
                }
            }
            else if(entity is LightEntity lightEntity)
            {
                // Select the light in the lighting service
                lightingService.SelectedLightEntity = lightEntity;
            }
        }
    }
}
