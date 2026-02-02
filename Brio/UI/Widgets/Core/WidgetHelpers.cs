using Brio.Capabilities.Core;
using Brio.Config;
using Brio.UI.Controls.Stateless;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Collections.Generic;
using System.Numerics;

namespace Brio.UI.Widgets.Core;

public class WidgetHelpers
{
    public static void DrawBodies(IEnumerable<Capability> capabilities)
    {
        foreach(var w in capabilities)
        {
            if(!w.Entity.IsAttached)
                break;

            DrawBody(w.Widget);
        }
    }

    public static void DrawBody(IWidget? widget)
    {
        if(widget == null || !widget.Flags.HasFlag(WidgetFlags.DrawBody))
            return;

        using(ImRaii.PushId(widget.GetType().Name))
        {
            ImGuiTreeNodeFlags treeFlags = ImGuiTreeNodeFlags.None;

            if(widget.Flags.HasFlag(WidgetFlags.DefaultOpen))
                treeFlags |= ImGuiTreeNodeFlags.DefaultOpen;

            treeFlags |= ImGuiTreeNodeFlags.AllowItemOverlap;

            string widgetKey = widget.GetType().Name;
            var config = ConfigurationService.Instance?.Configuration;
            uint? customTextColor = null;

            if(config?.Interface.WidgetDropdownTextColors != null
                && config.Interface.WidgetDropdownTextColors.TryGetValue(widgetKey, out var storedColor))
            {
                customTextColor = storedColor;
            }

            if(widget.Flags.HasFlag(WidgetFlags.HasAdvanced))
            {
                var startPos = ImGui.GetCursorPos();
                string tool = $"Advanced {widget.HeaderName}";

                if(ImBrio.FontIconButtonRight("advanced", FontAwesomeIcon.SquareArrowUpRight, 2, tool, bordered: false, size: new Vector2(23)))
                    widget.ToggleAdvancedWindow();

                ImGui.SetCursorPos(startPos);
            }

            bool isOpen = ImGui.CollapsingHeader(widget.HeaderName, treeFlags);
            if(config?.Interface.ShowColorPickers == true)
                DrawHeaderColorPicker(widgetKey, customTextColor, config);

            if(isOpen)
            {
                if(customTextColor.HasValue)
                {
                    using(ImRaii.PushColor(ImGuiCol.Text, customTextColor.Value))
                    {
                        widget.DrawBody();
                    }
                }
                else
                {
                    widget.DrawBody();
                }
            }
        }
    }

    private static void DrawHeaderColorPicker(string widgetKey, uint? customTextColor, Configuration? config)
    {
        if(config == null)
            return;

        var style = ImGui.GetStyle();
        bool hasScrollbar = ImGui.GetScrollMaxY() > 0;
        float scrollbarWidth = hasScrollbar ? style.ScrollbarSize : 0;

        var cursorPos = ImGui.GetWindowSize().X - scrollbarWidth - ((ImBrio.ScrollbarSize.X + (style.FramePadding.X * 2)) * 1f);
        ImGui.SameLine();
        ImGui.SetCursorPosX(cursorPos);

        uint baseColor = customTextColor ?? ImGui.GetColorU32(ImGuiCol.Text);
        Vector4 color = ImGui.ColorConvertU32ToFloat4(baseColor);

        ImGui.SetNextItemWidth(18 * ImGuiHelpers.GlobalScale);
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha;

        if(ImGui.ColorEdit4($"##{widgetKey}_text_color", ref color, flags))
        {
            config.Interface.WidgetDropdownTextColors[widgetKey] = ImGui.ColorConvertFloat4ToU32(color);
            ConfigurationService.Instance?.ApplyChange();
        }
    }

    public static void DrawQuickIcons(IEnumerable<Capability> capabilities)
    {
        bool drewAny = false;
        foreach(var w in capabilities)
        {
            if(!w.Entity.IsAttached)
                break;

            drewAny = true;
            DrawQuickIconSection(w);
            ImGui.SameLine();
        }

        if(drewAny)
            ImGui.NewLine();
    }

    public static void DrawQuickIconSection(Capability capability) => DrawQuickIconSection(capability.Widget);

    public static void DrawQuickIconSection(IWidget? widget)
    {
        if(widget == null || !widget.Flags.HasFlag(WidgetFlags.DrawQuickIcons))
            return;

        using(ImRaii.PushId(widget.GetType().Name))
        {
            widget.DrawQuickIcons();
        }
    }
}
