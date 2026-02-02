using Brio.Capabilities.Actor;
using Brio.Capabilities.Posing;
using Brio.Entities;
using Brio.Entities.Core;
using Brio.Game.GPose;
using Brio.Game.Posing;
using Brio.UI.Controls.Editors;
using Brio.UI.Controls.Stateless;
using Brio.UI.Theming;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Brio.Entities.Actor;

public class ActorContainerEntity(IServiceProvider provider) : Entity("actorContainer", provider)
{
    private readonly EntityManager _entityManager = provider.GetRequiredService<EntityManager>();
    private readonly GPoseService _gPoseService = provider.GetRequiredService<GPoseService>();

    public override string FriendlyName => "Actors";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Users;

    public override EntityFlags Flags => EntityFlags.HasContextButton;

    public override int ContextButtonCount => 3;

    public override void OnAttached()
    {
        AddCapability(ActivatorUtilities.CreateInstance<ActorContainerCapability>(_serviceProvider, this));
    }

    public override void OnChildAttached() => SortChildren();
    public override void OnChildDetached() => SortChildren();

    public override void DrawContextButton()
    {
        using(ImRaii.Disabled(_gPoseService.IsGPosing == false))
        {
            using(ImRaii.PushColor(ImGuiCol.Button, ThemeManager.CurrentTheme.Accent.AccentColor))
            {
                string modelTransformToolTip = "Select Model Transform for All Actors";
                if(ImBrio.FontIconButtonRight($"###{Id}_actors_model_transform", FontAwesomeIcon.LocationCrosshairs, 3f, modelTransformToolTip, bordered: false))
                {
                    _entityManager.ClearSelectedEntities();

                    foreach(var actor in _entityManager.TryGetAllActors())
                    {
                        _entityManager.AddSelectedEntity(actor.Id);

                        if(actor.TryGetCapability<PosingCapability>(out var posingCapability))
                        {
                            posingCapability.ClearSelection();
                            posingCapability.Selected = PosingSelectionType.ModelTransform;
                        }
                    }
                }

                ImGui.SameLine();
                string toolTip = $"New Actor";
                if(ImBrio.FontIconButtonRight($"###{Id}_actors_contextButton", FontAwesomeIcon.Plus, 2f, toolTip, bordered: false))
                {
                    ImGui.OpenPopup("ActorEditorDrawSpawnMenuPopup");
                }
                ActorEditor.DrawSpawnMenu(this);
            }
        }
    }

    private void SortChildren()
    {
        _children.Sort((a, b) =>
        {
            if(a is ActorEntity actorA && b is ActorEntity actorB)
                return actorA.GameObject.ObjectIndex.CompareTo(actorB.GameObject.ObjectIndex);

            return string.Compare(a.Id.Unique, b.Id.Unique, System.StringComparison.Ordinal);
        });
    }
}
