// SPDX-FileCopyrightText: 2022 Alex Evgrashin
// SPDX-FileCopyrightText: 2022 mirrorcult
// SPDX-FileCopyrightText: 2022 wrexbe
// SPDX-FileCopyrightText: 2023 DrSmugleaf
// SPDX-FileCopyrightText: 2025 Alkheemist
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Speech.Components;

/// <summary>
///     Applies accent to user while they wear entity as a clothing.
/// </summary>
[RegisterComponent]
public sealed partial class AddAccentClothingComponent : Component
{
    /// <summary>
    ///     Component name for accent that will be applied.
    /// </summary>
    [DataField("accent", required: true)]
    public string Accent = default!;

    /// <summary>
    ///     What <see cref="ReplacementAccentPrototype"/> to use.
    ///     Will be applied only with <see cref="ReplacementAccentComponent"/>.
    /// </summary>
    [DataField("replacement", customTypeSerializer: typeof(PrototypeIdSerializer<ReplacementAccentPrototype>))]
    public string? ReplacementPrototype;

    /// <summary>
    ///     Is that clothing is worn and affecting someones accent?
    /// </summary>
    public bool IsActive = false;

    /// <summary>
    ///     Who is currently wearing the item?
    /// </summary>
    public EntityUid Wearer; // Frontier
}
