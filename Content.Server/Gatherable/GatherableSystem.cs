// SPDX-FileCopyrightText: 2022 Chris V <HoofedEar@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Paul Ritter <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2023 forthbridge <79264743+forthbridge@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2024 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 VMSolidus <evilexecutive@gmail.com>
// SPDX-FileCopyrightText: 2024 sleepyyapril <flyingkarii@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server.Destructible;
using Content.Server.Gatherable.Components;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GatherableComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<GatherableComponent, AttackedEvent>(OnAttacked);
        InitializeProjectile();
    }

    private void OnAttacked(Entity<GatherableComponent> gatherable, ref AttackedEvent args)
    {
        if (_whitelistSystem.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.Used))
            return;

        Gather(gatherable, args.User);
    }

    private void OnActivate(Entity<GatherableComponent> gatherable, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (_whitelistSystem.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.User))
            return;

        Gather(args.Target, args.User, gatherable.Comp);
        args.Handled = true;
    }

    public void Gather(EntityUid gatheredUid, EntityUid? gatherer = null, GatherableComponent? component = null)
    {
        if (!Resolve(gatheredUid, ref component))
            return;

        if (TryComp<SoundOnGatherComponent>(gatheredUid, out var soundComp))
            _audio.PlayPvs(soundComp.Sound, Transform(gatheredUid).Coordinates);

        // Complete the gathering process
        _destructible.DestroyEntity(gatheredUid);

        // Spawn the loot!
        if (component.Loot == null)
            return;

        var pos = _transform.GetMapCoordinates(gatheredUid);

        foreach (var (tag, table) in component.Loot)
        {
            if (tag != "All")
            {
                if (gatherer != null && !_tagSystem.HasTag(gatherer.Value, tag))
                    continue;
            }
            var getLoot = _proto.Index(table);
            var spawnLoot = getLoot.GetSpawns(_random);
            var spawnPos = pos.Offset(_random.NextVector2(component.GatherOffset));
            Spawn(spawnLoot[0], spawnPos);
        }
    }
}
