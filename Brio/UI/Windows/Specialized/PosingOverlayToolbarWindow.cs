using Brio.Capabilities.Actor;
using Brio.Capabilities.Posing;
using Brio.Capabilities.World;
using Brio.Config;
using Brio.Core;
using Brio.Entities;
using Brio.Game.Camera;
using Brio.Game.Input;
using Brio.Game.Posing;
using Brio.Game.World;
using Brio.Input;
using Brio.Services;
using Brio.UI.Controls.Core;
using Brio.UI.Controls.Editors;
using Brio.UI.Controls.Stateless;
using Brio.UI.Theming;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using OneOf.Types;
using System;
using System.Numerics;

namespace Brio.UI.Windows.Specialized;

public class PosingOverlayToolbarWindow : Window
{
    private readonly PosingOverlayWindow _overlayWindow;
    private readonly EntityManager _entityManager;
    private readonly HistoryService _groupedUndoService;
    private readonly PosingTransformWindow _overlayTransformWindow;
    private readonly LightWindow _lightWindow;
    private readonly PosingService _posingService;
    private readonly ConfigurationService _configurationService;
    private readonly GameInputService _gameInputService;
    private readonly LightingService _lightingService;
    private readonly CameraService _cameraService;
    private readonly VirtualCameraManager _virtualCameraManager;

    private readonly IFramework _framework;

    private readonly BoneSearchControl _boneSearchControl = new();

    private bool _pushedStyle = false;

    private const string _boneFilterPopupName = "bone_filter_popup";

    public PosingOverlayToolbarWindow(PosingOverlayWindow overlayWindow, IFramework framework, LightWindow lightWindow, LightingService lightingService, HistoryService groupedUndoService, GameInputService gameInputService, EntityManager entityManager, PosingTransformWindow overlayTransformWindow, PosingService posingService, ConfigurationService configurationService, CameraService cameraService, VirtualCameraManager virtualCameraManager) : base($"{Brio.Name} OVERLAY###brio_posing_overlay_toolbar_window", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        Namespace = "brio_posing_overlay_toolbar_namespace";

        _overlayWindow = overlayWindow;
        _entityManager = entityManager;
        _overlayTransformWindow = overlayTransformWindow;
        _posingService = posingService;
        _configurationService = configurationService;
        _groupedUndoService = groupedUndoService;
        _gameInputService = gameInputService;
        _lightWindow = lightWindow;
        _lightingService = lightingService;
        _cameraService = cameraService;
        _virtualCameraManager = virtualCameraManager;
        _framework = framework;

        ShowCloseButton = false;
    }

    public override void PreOpenCheck()
    {
        IsOpen = _overlayWindow.IsOpen;

        _gameInputService.AllowEscape = true;

        if(UIManager.IsPosingGraphicalWindowOpen && _configurationService.Configuration.Posing.HideToolbarWhenAdvandedPosingOpen)
        {
            IsOpen = false;
        }

        base.PreOpenCheck();
    }

    public override bool DrawConditions()
    {
        _gameInputService.AllowEscape = true;

        return base.DrawConditions();
    }

    public override void PreDraw()
    {
        base.PreDraw();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        _pushedStyle = true;
    }

    public override void Draw()
    {
        if(_pushedStyle)
        {
            _pushedStyle = false;
            ImGui.PopStyleVar(1);
        }

        _entityManager.TryGetCapabilityFromSelectedEntity<PosingCapability>(out var posing);
        _entityManager.TryGetCapabilityFromSelectedEntity<ActionTimelineCapability>(out var timelineCapability);

        bool hasMultipleActorsSelected = _entityManager.SelectedEntityIds.Count > 1;

        if(posing?.Selected.Value is not null and BonePoseInfoId)
        {
            _gameInputService.AllowEscape = false;

            if(InputManagerService.ActionKeysPressed(InputAction.Posing_Esc))
            {
                posing.ClearSelection();
            }
        }
        else if(hasMultipleActorsSelected)
        {
            _gameInputService.AllowEscape = false;

            if(InputManagerService.ActionKeysPressed(InputAction.Posing_Esc))
            {
                var primaryEntityId = _entityManager.SelectedEntityId;
                if(primaryEntityId.HasValue)
                {
                    _entityManager.SetSelectedEntity(primaryEntityId.Value);
                }
            }
        }
        else
        {
            _gameInputService.AllowEscape = true;
        }

        if(posing is not null)
        {
            DrawButtons(posing, timelineCapability, hasMultipleActorsSelected);
            DrawBoneFilterPopup();
        }
        else if(_lightingService.SelectedLightEntity is not null)
        {
            _lightingService.SelectedLightEntity.TryGetCapability<LightTransformCapability>(out var lightCap);
            DrawLightButtons(lightCap);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.Transparent);

            using(ImRaii.PushColor(ImGuiCol.Text, _overlayTransformWindow.IsOpen ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
            {
                using(ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if(ImGui.Button($"{FontAwesomeIcon.LocationCrosshairs.ToIconString()}###toggle_transforms_window", button3XSizeVector2))
                        _overlayTransformWindow.IsOpen = !_overlayTransformWindow.IsOpen;
                }
            }
            ImBrio.AttachToolTip("Toggle Transform Window");

            ImGui.SameLine();

            using(ImRaii.PushColor(ImGuiCol.Text, _lightWindow.IsOpen ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
            {
                using(ImRaii.PushFont(UiBuilder.IconFont))
                {
                    if(ImGui.Button($"{FontAwesomeIcon.Lightbulb.ToIconString()}###toggle_light_window", button3XSizeVector2))
                        _lightWindow.IsOpen = !_lightWindow.IsOpen;
                }
            }
            ImBrio.AttachToolTip("Toggle Light Window");

            ImGui.SameLine();

            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.WindowClose.ToIconString()}###close_overlay", button3XSizeVector2))
                    _overlayWindow.IsOpen = false;
            }
            ImBrio.AttachToolTip("Close Overlay");

            ImGui.PopStyleColor();

            ImGui.TextColored(ImGuiColors.DalamudRed, "Attention! No valid,");
            ImGui.TextColored(ImGuiColors.DalamudRed, "Actor or Light Selected!");
            ImGui.TextColored(ImGuiColors.DalamudRed, "Please selected one,");
            ImGui.TextColored(ImGuiColors.DalamudRed, "in the Scene Manager!");
        }
    }

    public override void PostDraw()
    {
        if(_pushedStyle)
        {
            _pushedStyle = false;
            ImGui.PopStyleVar(1);
        }

        base.PostDraw();
    }

    public float button3XSize => ImGui.GetTextLineHeight() * 3.2f;
    public float button4XSize => ImGui.GetTextLineHeight() * 2.4f;
    public float button2XSize => ImGui.GetTextLineHeight() * 5f;

    public Vector2 button2XSizeVector2 => new(button2XSize, button2XSize / 2f);
    public Vector2 button3x3Vector2 => new(button3XSize, button3XSize / 1.2f);
    public Vector2 button3XSizeVector2 => new(button3XSize, button3XSize / 1.2f);
    public Vector2 button4XSizeVector2 => new(button4XSize, button3XSize);


    // This is awful I hate it. but I need this done fast (FAST)
    public unsafe void DrawLightButtons(LightTransformCapability? lightTransformCapability)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.Transparent);

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{(_lightingService.CoordinateMode == LightGizmoCoordinateMode.Local ? FontAwesomeIcon.Globe.ToIconString() : FontAwesomeIcon.Atom.ToIconString())}###select_mode", button3XSizeVector2) || InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_ToggleWorld))
                _lightingService.CoordinateMode = _lightingService.CoordinateMode == LightGizmoCoordinateMode.Local ? LightGizmoCoordinateMode.World : LightGizmoCoordinateMode.Local;
        }
        ImBrio.AttachToolTip(_lightingService.CoordinateMode == LightGizmoCoordinateMode.Local ? "Switch to World" : "Switch to Local");

        ImGui.SameLine();

        using(ImRaii.PushColor(ImGuiCol.Text, _lightWindow.IsOpen ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Lightbulb.ToIconString()}###toggle_light_window", button3XSizeVector2))
                    _lightWindow.IsOpen = !_lightWindow.IsOpen;
            }
        }
        ImBrio.AttachToolTip("Toggle Light Window");

        ImGui.SameLine();

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{FontAwesomeIcon.WindowClose.ToIconString()}###close_overlay", button3XSizeVector2))
                _overlayWindow.IsOpen = false;
        }
        ImBrio.AttachToolTip("Close Overlay");

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        using(ImRaii.PushColor(ImGuiCol.Text, _lightingService.Operation == LightGizmoOperation.Translate ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.ArrowsUpDownLeftRight.ToIconString()}###select_position", button3XSizeVector2) || InputManagerService.ActionKeysPressed(InputAction.Posing_Translate))
                    _lightingService.Operation = LightGizmoOperation.Translate;
            }
        }
        ImBrio.AttachToolTip("Position");

        ImGui.SameLine();


        using(ImRaii.PushColor(ImGuiCol.Text, _lightingService.Operation == LightGizmoOperation.Rotate ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.ArrowsSpin.ToIconString()}###select_rotate", button3XSizeVector2) || InputManagerService.ActionKeysPressed(InputAction.Posing_Rotate))
                    _lightingService.Operation = LightGizmoOperation.Rotate;
            }
        }
        ImBrio.AttachToolTip("Rotation");

        ImGui.SameLine();

        using(ImRaii.PushColor(ImGuiCol.Text, _lightingService.Operation == LightGizmoOperation.Universal ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Cubes.ToIconString()}###select_universal", button3XSizeVector2) || InputManagerService.ActionKeysPressed(InputAction.Posing_Universal))
                {
                    _lightingService.Operation = LightGizmoOperation.Universal;
                }
            }
        }
        ImBrio.AttachToolTip("Universal");
        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        // Undo Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(!lightTransformCapability?.CanUndo ?? false))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Backward.ToIconString()}###undo_pose", button3XSizeVector2) || (InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_Undo) && (lightTransformCapability?.CanUndo ?? false)))
                {
                    lightTransformCapability?.Undo();
                }
            }
        }
        ImBrio.AttachToolTip("Undo last Light Action");

        ImGui.SameLine();

        // Redo Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(!lightTransformCapability?.CanRedo ?? false))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Forward.ToIconString()}###redo_pose", button3XSizeVector2) || (InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_Redo) && (lightTransformCapability?.CanRedo ?? false)))
                {
                    lightTransformCapability?.Redo();
                }
            }
        }
        ImBrio.AttachToolTip("Redo last Light Action");

        ImGui.SameLine();

        // Reset Pose Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(!lightTransformCapability?.HasOverride ?? false))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Undo.ToIconString()}###reset_pose", button3XSizeVector2))
                    lightTransformCapability?.Reset(false, false);
            }
        }
        ImBrio.AttachToolTip("Reset Light Transform");

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        // Load Pose Button

        using(ImRaii.Disabled(true))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.FileDownload.ToIconString()}###import_pose", button2XSizeVector2))
                {

                }
            }
            ImBrio.AttachToolTip("Load Light from Clipboard");

            ImGui.SameLine();

            // Save Pose Button

            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Save.ToIconString()}###export_pose", button2XSizeVector2))
                {

                }
            }
            ImBrio.AttachToolTip("Save Light to Clipboard");
        }

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        // Move to Camera Button
        var hasCameraForLight = _virtualCameraManager.CurrentCamera is not null;
        using(ImRaii.Disabled(!hasCameraForLight))
        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{FontAwesomeIcon.Video.ToIconString()}###move_to_camera", new Vector2(ImGui.GetContentRegionAvail().X, button3XSize / 1.2f)))
            {
                if(lightTransformCapability is not null && _virtualCameraManager.CurrentCamera is not null)
                {
                    var camera = _cameraService.GetCurrentCamera();
                    if(camera != null)
                    {
                        Vector3 cameraPos;
                        Quaternion cameraRotation;
                        
                        // Check if using Free-Cam or regular camera
                        if(_virtualCameraManager.CurrentCamera.IsFreeCamera)
                        {
                            // For Free-Cam, use both Position and Rotation from VirtualCamera
                            cameraPos = _virtualCameraManager.CurrentCamera.Position;
                            var rotation = _virtualCameraManager.CurrentCamera.Rotation;
                            
                            // Create quaternion from the Free-Cam's rotation angles and add 180 degree yaw to flip direction
                            cameraRotation = Quaternion.CreateFromYawPitchRoll(rotation.X + MathF.PI, rotation.Y, rotation.Z);
                        }
                        else
                        {
                            // For regular cameras, get position from actual camera pointer and use LookAtVector
                            cameraPos = camera->GetPosition();
                            var cameraTarget = (Vector3)camera->Camera.CameraBase.SceneCamera.LookAtVector;
                            var forwardVector = Vector3.Normalize(cameraTarget - cameraPos);
                            
                            // Create a rotation quaternion from the forward direction
                            var upVector = new Vector3(0, 1, 0);
                            var rightVector = Vector3.Normalize(Vector3.Cross(upVector, forwardVector));
                            var adjustedUpVector = Vector3.Cross(forwardVector, rightVector);
                            
                            // Create rotation matrix and convert to quaternion
                            var rotationMatrix = new Matrix4x4(
                                rightVector.X, rightVector.Y, rightVector.Z, 0,
                                adjustedUpVector.X, adjustedUpVector.Y, adjustedUpVector.Z, 0,
                                forwardVector.X, forwardVector.Y, forwardVector.Z, 0,
                                0, 0, 0, 1
                            );
                            cameraRotation = Quaternion.CreateFromRotationMatrix(rotationMatrix);
                        }
                        
                        lightTransformCapability.Snapshot();
                        lightTransformCapability.position = cameraPos;
                        lightTransformCapability.rotation = cameraRotation.ToEuler();
                        lightTransformCapability.Transform.Position = cameraPos;
                        lightTransformCapability.Transform.Rotation = cameraRotation;
                        lightTransformCapability.Light.GameLight.GameLight->Transform.Position = cameraPos;
                        lightTransformCapability.Light.GameLight.GameLight->Transform.Rotation = cameraRotation;
                        lightTransformCapability.Light.GameLight.GameLight->Transform.Rotation = cameraRotation;
                    }
                }
            }
        }
        ImBrio.AttachToolTip(hasCameraForLight ? "Move Light to Camera Position and Rotation" : "No Camera Available");

        ImGui.PopStyleColor();
    }

    private unsafe void DrawButtons(PosingCapability? posing, ActionTimelineCapability? timelineCapability, bool hasMultipleActorsSelected)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, UIConstants.Transparent);

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{(_posingService.CoordinateMode == PosingCoordinateMode.Local ? FontAwesomeIcon.Globe.ToIconString() : FontAwesomeIcon.Atom.ToIconString())}###select_mode", button4XSizeVector2) || InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_ToggleWorld))
                _posingService.CoordinateMode = _posingService.CoordinateMode == PosingCoordinateMode.Local ? PosingCoordinateMode.World : PosingCoordinateMode.Local;
        }
        ImBrio.AttachToolTip(_posingService.CoordinateMode == PosingCoordinateMode.Local ? "Switch to World" : "Switch to Local");

        ImGui.SameLine();

        using(ImRaii.PushColor(ImGuiCol.Text, _overlayTransformWindow.IsOpen ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.LocationCrosshairs.ToIconString()}###toggle_transforms_window", button4XSizeVector2))
                    _overlayTransformWindow.IsOpen = !_overlayTransformWindow.IsOpen;
            }
        }
        ImBrio.AttachToolTip("Toggle Transform Window");

        ImGui.SameLine();

        using(ImRaii.PushColor(ImGuiCol.Text, _lightWindow.IsOpen ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Lightbulb.ToIconString()}###toggle_light_window", button4XSizeVector2))
                    _lightWindow.IsOpen = !_lightWindow.IsOpen;
            }
        }
        ImBrio.AttachToolTip("Toggle Light Window");


        ImGui.SameLine();

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{FontAwesomeIcon.WindowClose.ToIconString()}###close_overlay", button4XSizeVector2))
                _overlayWindow.IsOpen = false;
        }
        ImBrio.AttachToolTip("Close Overlay");

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        using(ImRaii.PushColor(ImGuiCol.Text, _posingService.Operation == PosingOperation.Translate ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.ArrowsUpDownLeftRight.ToIconString()}###select_position", new Vector2(button4XSize)) || InputManagerService.ActionKeysPressed(InputAction.Posing_Translate))
                    _posingService.Operation = PosingOperation.Translate;
            }
        }
        ImBrio.AttachToolTip("Position");

        ImGui.SameLine();


        using(ImRaii.PushColor(ImGuiCol.Text, _posingService.Operation == PosingOperation.Rotate ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.ArrowsSpin.ToIconString()}###select_rotate", new Vector2(button4XSize)) || InputManagerService.ActionKeysPressed(InputAction.Posing_Rotate))
                    _posingService.Operation = PosingOperation.Rotate;
            }
        }
        ImBrio.AttachToolTip("Rotation");

        ImGui.SameLine();

        using(ImRaii.PushColor(ImGuiCol.Text, _posingService.Operation == PosingOperation.Scale ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.ExpandAlt.ToIconString()}###select_scale", new Vector2(button4XSize)) || InputManagerService.ActionKeysPressed(InputAction.Posing_Scale))
                    _posingService.Operation = PosingOperation.Scale;
            }
        }
        ImBrio.AttachToolTip("Scale");

        ImGui.SameLine();

        using(ImRaii.PushColor(ImGuiCol.Text, _posingService.Operation == PosingOperation.Universal ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
        {
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Cubes.ToIconString()}###select_universal", new Vector2(button4XSize)) || InputManagerService.ActionKeysPressed(InputAction.Posing_Universal))
                {
                    _posingService.Operation = PosingOperation.Universal;
                }
            }
        }
        ImBrio.AttachToolTip("Universal");

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        var bone = posing?.Selected.Match(
              boneSelect => posing.SkeletonPosing.GetBone(boneSelect),
              _ => null,
              _ => null
            ) ?? null;

        using(ImRaii.Disabled(posing is null))
        {

            // IK Button
            bool enabled = false;
            if(posing?.Selected.Value is BonePoseInfoId boneId)
            {
                var bonePose = posing.SkeletonPosing.GetBonePose(boneId);
                var ik = bonePose.DefaultIK;
                enabled = ik.Enabled && BrioStyle.EnableStyle;
            }

            using(ImRaii.Disabled(!(bone?.EligibleForIK == true)))
            {
                using(ImRaii.PushColor(ImGuiCol.Button, ThemeManager.CurrentTheme.Accent.AccentColor, enabled))
                {
                    if(ImGui.Button($"IK###bone_ik", new Vector2(button4XSize)))
                        ImGui.OpenPopup("overlay_bone_ik");
                }
            }
            ImBrio.AttachToolTip("Inverse Kinematics");

            ImGui.SameLine();

            // Bone Filter Button

            using(ImRaii.Disabled(hasMultipleActorsSelected))
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Bone.ToIconString()}###toggle_filter_window", new Vector2(button4XSize)))
                    ImGui.OpenPopup(_boneFilterPopupName);
            }
            ImBrio.AttachToolTip("Bone Filter");

            ImGui.SameLine();

            // Select Parent Button

            var parentBone = bone?.Parent;

            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                using(ImRaii.Disabled(parentBone == null))
                {
                    if(ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}###select_parent", new Vector2(button4XSize)))
                        posing?.Selected = new BonePoseInfoId(parentBone!.Name, parentBone!.PartialId, PoseInfoSlot.Character);
                }
            }
            ImBrio.AttachToolTip("Select Parent");

            ImGui.SameLine();

            // MirrorMode Button

            PosingEditorCommon.DrawMirrorModeSelect(posing, new Vector2(button4XSize));

            //
            // -------------
            //

            ImBrio.VerticalPadding(5);

            // Set IK Button

            using(ImRaii.Disabled(posing?.SkeletonPosing.PoseInfo.HasIKStacks is false))
            {
                using(ImRaii.PushFont(UiBuilder.IconFont))
                    if(ImGui.Button($"{FontAwesomeIcon.Lock.ToIconString()}###clear_ik", new Vector2(button4XSize)))
                        posing?.SkeletonPosing.ResetIK();
                ImBrio.AttachToolTip($"Set IK Changes{(!posing?.SkeletonPosing.PoseInfo.HasIKStacks ?? false ? ". Enable IK & make a change with IK to 'Lock in' IK changes using this button." : "")}");

                var center = ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() / 2);
                var radius = MathF.Ceiling(ImGui.GetTextLineHeight() * 0.9f);
                var thickness = MathF.Ceiling(ImGui.GetTextLineHeight() * 0.1f);

                ImGui.GetWindowDrawList().AddCircle(center, radius, ImGui.GetColorU32(ImGuiCol.Text) & 0x80FFFFFF, 32, thickness);

                if(posing?.SkeletonPosing.PoseInfo.HasIKStacks is false)
                {
                    thickness += 0.2f;
                    var offset = (radius - thickness) / MathF.Sqrt(2.0f);
                    var lineStart = center + new Vector2(-offset, -offset);
                    var lineEnd = center + new Vector2(offset, offset);
                    ImGui.GetWindowDrawList().AddLine(lineStart, lineEnd, 0x400000FF, thickness);
                }
            }

            ImGui.SameLine();

            // Bone Search Button

            using(ImRaii.Disabled(hasMultipleActorsSelected))
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Search.ToIconString()}###bone_search", new Vector2(button4XSize)))
                    ImGui.OpenPopup("overlay_bone_search_popup");
            }
            ImBrio.AttachToolTip("Bone Search");

            ImGui.SameLine();

            // Clear Button

            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                using(ImRaii.Disabled(posing?.Selected.Value is None))
                {
                    if(ImGui.Button($"{FontAwesomeIcon.MinusSquare.ToIconString()}###clear_selected", new Vector2(button4XSize)))
                    {
                        if(hasMultipleActorsSelected)
                        {
                            var primaryEntityId = _entityManager.SelectedEntityId;
                            if(primaryEntityId.HasValue)
                            {
                                _entityManager.SetSelectedEntity(primaryEntityId.Value);
                            }
                        }
                        else
                        {
                            posing?.ClearSelection();
                        }
                    }
                }
            }
            ImBrio.AttachToolTip("Clear Selection");

            ImGui.SameLine();

            // Freeze Button

            bool allFrozen = true;
            if(hasMultipleActorsSelected)
            {
                foreach(var entityId in _entityManager.SelectedEntityIds)
                {
                    if(_entityManager.TryGetEntity(entityId, out var entity))
                    {
                        if(entity.TryGetCapability<ActionTimelineCapability>(out var cap))
                        {
                            if(cap.SpeedMultiplierOverride != 0)
                            {
                                allFrozen = false;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                allFrozen = timelineCapability?.SpeedMultiplierOverride == 0;
            }

            using(ImRaii.PushColor(ImGuiCol.Text, allFrozen ? UIConstants.ToggleButtonActive : UIConstants.ToggleButtonInactive))
            using(ImRaii.PushFont(UiBuilder.IconFont))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Snowflake.ToIconString()}###freeze_toggle", new Vector2(button4XSize)) || InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_Freeze))
                {
                    if(hasMultipleActorsSelected)
                    {
                        bool shouldFreeze = !allFrozen;
                        foreach(var entityId in _entityManager.SelectedEntityIds)
                        {
                            if(_entityManager.TryGetEntity(entityId, out var entity))
                            {
                                if(entity.TryGetCapability<ActionTimelineCapability>(out var cap))
                                {
                                    if(shouldFreeze)
                                        cap.SetOverallSpeedOverride(0f);
                                    else
                                        cap.ResetOverallSpeedOverride();
                                }
                            }
                        }
                    }
                    else
                    {
                        if(timelineCapability?.SpeedMultiplierOverride == 0)
                            timelineCapability?.ResetOverallSpeedOverride();
                        else
                            timelineCapability?.SetOverallSpeedOverride(0f);
                    }
                }
            }
            ImBrio.AttachToolTip($"{(allFrozen ? "Un-" : "")}Freeze Character{(hasMultipleActorsSelected ? "s" : "")}");

        }

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        // Undo Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(!posing?.CanUndo ?? false))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Backward.ToIconString()}###undo_pose", new Vector2(button4XSize)) || (InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_Undo) && (posing?.CanUndo ?? false)))
                {
                    posing?.Undo();
                }
            }
        }
        ImBrio.AttachToolTip("Undo");

        ImGui.SameLine();

        // Redo Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(!posing?.CanRedo ?? false))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Forward.ToIconString()}###redo_pose", new Vector2(button4XSize)) || (InputManagerService.ActionKeysPressedLastFrame(InputAction.Posing_Redo) && (posing?.CanRedo ?? false)))
                {
                    posing?.Redo();
                }
            }
        }
        ImBrio.AttachToolTip("Redo");

        ImGui.SameLine();

        // Reset Pose Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(hasMultipleActorsSelected || (!posing?.CanResetBone(bone) ?? false)))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Retweet.ToIconString()}###reset_bone", new Vector2(button4XSize)))
                    posing?.ResetSelectedBone();
            }
        }
        ImBrio.AttachToolTip("Reset Bone");

        ImGui.SameLine();

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(hasMultipleActorsSelected))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Repeat.ToIconString()}###MirrorPose", new Vector2(button4XSize)))
                    posing?.MirrorPose();
            }
        }
        ImBrio.AttachToolTip("Mirror Pose");

        //

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(!posing.HasOverride(posing.SkeletonPosing.FilterNonFaceBones)))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Undo.ToIconString()}###reset_body_pose", button3x3Vector2))
                {
                    posing.Snapshot(false, reconcile: false);
                    posing.ClearStacks(posing.SkeletonPosing.FilterNonFaceBones);
                }

                ImGui.GetWindowDrawList().AddText(ImGui.GetItemRectMin() + ImGui.GetItemRectSize() / 2, ImGui.GetColorU32(ImGuiCol.Text), FontAwesomeIcon.ChildReaching.ToIconString());

            }
        }

        ImBrio.AttachToolTip("Reset Body");

        ImGui.SameLine();

        using(ImRaii.PushFont(UiBuilder.IconFont))
        using(ImRaii.Disabled(!posing.HasOverride(posing.SkeletonPosing.FilterFaceBones)))
        {
            if(ImGui.Button($"{FontAwesomeIcon.Undo.ToIconString()}###reset_face_pose", button3x3Vector2))
            {
                posing.ClearStacks(posing.SkeletonPosing.FilterFaceBones);
            }

            ImGui.GetWindowDrawList().AddText(ImGui.GetItemRectMin() + ImGui.GetItemRectSize() / 2, ImGui.GetColorU32(ImGuiCol.Text), FontAwesomeIcon.Smile.ToIconString());

        }

        ImBrio.AttachToolTip("Reset Face");

        ImGui.SameLine();

        // Reset Pose Button

        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            using(ImRaii.Disabled(hasMultipleActorsSelected || (!posing?.HasOverride() ?? false)))
            {
                if(ImGui.Button($"{FontAwesomeIcon.Undo.ToIconString()}###reset_pose", button3x3Vector2))
                    posing?.Reset(false, false);
            }
        }
        ImBrio.AttachToolTip("Reset Pose");

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        // Load Pose Button

        using(ImRaii.Disabled(hasMultipleActorsSelected))
        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{FontAwesomeIcon.FileDownload.ToIconString()}###import_pose", button2XSizeVector2))
                ImGui.OpenPopup("DrawImportPoseMenuPopup");
        }
        ImBrio.AttachToolTip("Import Pose");

        ImGui.SameLine();

        // Save Pose Button

        using(ImRaii.Disabled(hasMultipleActorsSelected))
        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{FontAwesomeIcon.Save.ToIconString()}###export_pose", button2XSizeVector2))
                FileUIHelpers.ShowExportPoseModal(posing);
        }
        ImBrio.AttachToolTip("Save Pose");

        //
        // -------------
        //

        ImBrio.VerticalPadding(5);
        ImGui.Separator();
        ImBrio.VerticalPadding(5);

        // Move to Camera Button
        var hasCameraForActor = _virtualCameraManager.CurrentCamera is not null;
        using(ImRaii.Disabled(hasMultipleActorsSelected || !hasCameraForActor))
        using(ImRaii.PushFont(UiBuilder.IconFont))
        {
            if(ImGui.Button($"{FontAwesomeIcon.Video.ToIconString()}###move_actor_to_camera", new Vector2(ImGui.GetContentRegionAvail().X, button3XSize / 1.2f)))
            {
                if(posing is not null)
                {
                    var camera = _cameraService.GetCurrentCamera();
                    if(camera != null)
                    {
                        // Get actual camera position in 3D space (accounts for zoom/distance)
                        var cameraPos = camera->GetPosition();
                        posing.Snapshot();
                        posing.ModelPosing.Transform = posing.ModelPosing.Transform with { Position = cameraPos };
                    }
                }
            }
        }
        ImBrio.AttachToolTip(hasCameraForActor ? "Move Actor to Camera Position" : "No Camera Available");

        ImGui.PopStyleColor();

        FileUIHelpers.DrawImportPoseMenuPopup("postingOverlay", posing, true);

        using(var popup = ImRaii.Popup("overlay_bone_search_popup"))
        {
            if(popup.Success)
            {
                _boneSearchControl.Draw("overlay_bone_search", posing!);
            }
        }

        using(var popup = ImRaii.Popup("overlay_bone_ik"))
        {
            if(popup.Success)
            {
                if(posing?.Selected.Value is BonePoseInfoId id)
                {
                    var info = posing.SkeletonPosing.GetBonePose(id);
                    BoneIKEditor.Draw(info, posing);
                }
            }
        }
    }

    private void DrawBoneFilterPopup()
    {
        using var popup = ImRaii.Popup(_boneFilterPopupName);
        if(popup.Success)
        {
            PosingEditorCommon.DrawBoneFilterEditor(_posingService.OverlayFilter, _posingService);
        }
    }
}
