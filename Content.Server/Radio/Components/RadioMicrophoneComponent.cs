// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server.Radio.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server.Radio.Components;

/// <summary>
///     Listens for local chat messages and relays them to some radio frequency
/// </summary>
[RegisterComponent]
[Access(typeof(RadioDeviceSystem))]
public sealed partial class RadioMicrophoneComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("broadcastChannel", customTypeSerializer: typeof(PrototypeIdSerializer<RadioChannelPrototype>))]
    public string BroadcastChannel = SharedChatSystem.CommonChannel;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("listenRange")]
    public int ListenRange  = 4;

    [DataField("enabled")]
    public bool Enabled = false;

    [DataField("powerRequired")]
    public bool PowerRequired = false;

    /// <summary>
    /// Whether or not interacting with this entity
    /// toggles it on or off.
    /// </summary>
    [DataField("toggleOnInteract")]
    public bool ToggleOnInteract = true;

    /// <summary>
    /// Whether or not the speaker must have an
    /// unobstructed path to the radio to speak
    /// </summary>
    [DataField("unobstructedRequired")]
    public bool UnobstructedRequired = false;

    // Nuclear-14
    /// <summary>
    // The radio frequency on which the message will be transmitted
    /// </summary>
    [DataField]
    public int Frequency = 1459; // Common channel frequency
}
