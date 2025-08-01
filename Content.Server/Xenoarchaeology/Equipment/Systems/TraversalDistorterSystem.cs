// SPDX-FileCopyrightText: 2022 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+emogarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 VMSolidus <evilexecutive@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server.Construction;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Xenoarchaeology.Equipment.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Placeable;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Xenoarchaeology.Equipment.Systems;

public sealed class TraversalDistorterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<TraversalDistorterComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<TraversalDistorterComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<TraversalDistorterComponent, RefreshPartsEvent>(OnRefreshParts);
        SubscribeLocalEvent<TraversalDistorterComponent, UpgradeExamineEvent>(OnUpgradeExamine);

        SubscribeLocalEvent<TraversalDistorterComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<TraversalDistorterComponent, ItemRemovedEvent>(OnItemRemoved);
    }

    private void OnInit(EntityUid uid, TraversalDistorterComponent component, MapInitEvent args)
    {
        component.NextActivation = _timing.CurTime;
    }

    /// <summary>
    /// Switches the state of the traversal distorter between up and down.
    /// </summary>
    /// <param name="uid">The distorter's entity</param>
    /// <param name="component">The component on the entity</param>
    /// <returns>If the distorter changed state</returns>
    public bool SetState(EntityUid uid, TraversalDistorterComponent component, bool isDown)
    {
        if (!this.IsPowered(uid, EntityManager))
            return false;

        if (_timing.CurTime < component.NextActivation)
            return false;

        component.NextActivation = _timing.CurTime + component.ActivationDelay;

        component.BiasDirection = isDown ? BiasDirection.Down : BiasDirection.Up;

        return true;
    }

    private void OnExamine(EntityUid uid, TraversalDistorterComponent component, ExaminedEvent args)
    {
        string examine = string.Empty;
        switch (component.BiasDirection)
        {
            case BiasDirection.Up:
                examine = Loc.GetString("traversal-distorter-desc-up");
                break;
            case BiasDirection.Down:
                examine = Loc.GetString("traversal-distorter-desc-down");
                break;
        }
        args.PushMarkup(examine);
    }

    private void OnRefreshParts(EntityUid uid, TraversalDistorterComponent component, RefreshPartsEvent args)
    {
        var biasRating = args.PartRatings[component.MachinePartBiasChance];

        component.BiasChance = component.BaseBiasChance * MathF.Pow(component.PartRatingBiasChance, biasRating - 1);
    }

    private void OnUpgradeExamine(EntityUid uid, TraversalDistorterComponent component, UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("traversal-distorter-upgrade-bias", component.BiasChance / component.BaseBiasChance);
    }

    private void OnItemPlaced(EntityUid uid, TraversalDistorterComponent component, ref ItemPlacedEvent args)
    {
        var bias = EnsureComp<BiasedArtifactComponent>(args.OtherEntity);
        bias.Provider = uid;
    }

    private void OnItemRemoved(EntityUid uid, TraversalDistorterComponent component, ref ItemRemovedEvent args)
    {
        var otherEnt = args.OtherEntity;
        if (TryComp<BiasedArtifactComponent>(otherEnt, out var bias) && bias.Provider == uid)
            RemComp(otherEnt, bias);
    }
}
