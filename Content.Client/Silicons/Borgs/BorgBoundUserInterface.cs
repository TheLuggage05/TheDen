// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Shared.Silicons.Borgs;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.Borgs;

[UsedImplicitly]
public sealed class BorgBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BorgMenu? _menu;

    public BorgBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<BorgMenu>();
        _menu.SetEntity(Owner);

        _menu.BrainButtonPressed += () =>
        {
            SendMessage(new BorgEjectBrainBuiMessage());
        };

        _menu.EjectBatteryButtonPressed += () =>
        {
            SendMessage(new BorgEjectBatteryBuiMessage());
        };

        _menu.NameChanged += name =>
        {
            SendMessage(new BorgSetNameBuiMessage(name));
        };

        _menu.RemoveModuleButtonPressed += module =>
        {
            SendMessage(new BorgRemoveModuleBuiMessage(EntMan.GetNetEntity(module)));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not BorgBuiState msg)
            return;
        _menu?.UpdateState(msg);
    }
}
