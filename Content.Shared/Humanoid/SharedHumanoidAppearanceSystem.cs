// SPDX-FileCopyrightText: 2023 Alex Evgrashin
// SPDX-FileCopyrightText: 2023 Debug
// SPDX-FileCopyrightText: 2023 DrSmugleaf
// SPDX-FileCopyrightText: 2023 Flipp Syder
// SPDX-FileCopyrightText: 2023 Leon Friedrich
// SPDX-FileCopyrightText: 2023 Morb
// SPDX-FileCopyrightText: 2023 Visne
// SPDX-FileCopyrightText: 2023 Vordenburg
// SPDX-FileCopyrightText: 2023 csqrb
// SPDX-FileCopyrightText: 2024 Aiden
// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT
// SPDX-FileCopyrightText: 2024 FoxxoTrystan
// SPDX-FileCopyrightText: 2024 Tayrtahn
// SPDX-FileCopyrightText: 2024 VMSolidus
// SPDX-FileCopyrightText: 2024 deltanedas
// SPDX-FileCopyrightText: 2024 gluesniffler
// SPDX-FileCopyrightText: 2024 ike709
// SPDX-FileCopyrightText: 2024 metalgearsloth
// SPDX-FileCopyrightText: 2025 Aikakakah
// SPDX-FileCopyrightText: 2025 Falcon
// SPDX-FileCopyrightText: 2025 Lyndomen
// SPDX-FileCopyrightText: 2025 Tabitha
// SPDX-FileCopyrightText: 2025 Timfa
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.IO;
using System.Linq;
using System.Numerics;
using Content.Shared._DEN.Species;
using Content.Shared._EE.Contractors.Prototypes;
using Content.Shared.Decals;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared._Shitmed.Humanoid.Events; // Shitmed Change
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Content.Shared.HeightAdjust;
using Microsoft.Extensions.Configuration;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Shadowkin;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Shared.Humanoid;

/// <summary>
///     HumanoidSystem. Primarily deals with the appearance and visual data
///     of a humanoid entity. HumanoidVisualizer is what deals with actually
///     organizing the sprites and setting up the sprite component's layers.
///
///     This is a shared system, because while it is server authoritative,
///     you still need a local copy so that players can set up their
///     characters.
/// </summary>
public abstract class SharedHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] private readonly HeightAdjustSystem _heightAdjust = default!;

    [ValidatePrototypeId<SpeciesPrototype>]
    public const string DefaultSpecies = "Human";

    [ValidatePrototypeId<EmployerPrototype>]
    public const string DefaultEmployer = "NanoTrasen";

    [ValidatePrototypeId<NationalityPrototype>]
    public const string DefaultNationality = "Bieselite";

    [ValidatePrototypeId<LifepathPrototype>]
    public const string DefaultLifepath = "Spacer";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ExaminedEvent>(OnExamined);
    }

    public DataNode ToDataNode(HumanoidCharacterProfile profile)
    {
        var export = new HumanoidProfileExport
        {
            ForkId = _cfgManager.GetCVar(CVars.BuildForkId),
            Profile = profile,
        };

        var dataNode = _serManager.WriteValue(export, alwaysWrite: true, notNullableOverride: true);
        return dataNode;
    }

    public HumanoidCharacterProfile FromStream(Stream stream, ICommonSession session)
    {
        using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        var root = yamlStream.Documents[0].RootNode;
        var export = _serManager.Read<HumanoidProfileExport>(root.ToDataNode(), notNullableOverride: true);

        // Add custom handling here for forks / version numbers if you care

        var profile = export.Profile;
        var collection = IoCManager.Instance;
        profile.EnsureValid(session, collection!);
        return profile;
    }

    private void OnInit(EntityUid uid, HumanoidAppearanceComponent humanoid, ComponentInit args)
    {
        if (string.IsNullOrEmpty(humanoid.Species) || _netManager.IsClient && !IsClientSide(uid))
            return;

        if (string.IsNullOrEmpty(humanoid.Initial)
            || !_proto.TryIndex(humanoid.Initial, out HumanoidProfilePrototype? startingSet))
        {
            LoadProfile(uid, HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species), humanoid);
            return;
        }

        // Do this first, because profiles currently do not support custom base layers
        foreach (var (layer, info) in startingSet.CustomBaseLayers)
            humanoid.CustomBaseLayers.Add(layer, info);

        LoadProfile(uid, startingSet.Profile, humanoid);
    }

    private void OnExamined(EntityUid uid, HumanoidAppearanceComponent component, ExaminedEvent args)
    {
        var identity = Identity.Entity(uid, EntityManager);
        var species = GetSpeciesRepresentation(component.Species, component.CustomSpecieName).ToLower();
        var age = GetAgeRepresentation(component.Species, component.Age);
        if (HasComp<ShadowkinComponent>(uid))
        {
            var color = component.EyeColor.Name();
            if (color != null)
                age = Loc.GetString("identity-eye-shadowkin", ("color", color));
        }

        args.PushText(Loc.GetString("humanoid-appearance-component-examine", ("user", identity), ("age", age), ("species", species)));

        if (component.DisplayPronouns != null)
            args.PushText(Loc.GetString("humanoid-appearance-component-examine-pronouns", ("user", identity), ("pronouns", component.DisplayPronouns)));
    }

    /// <summary>
    ///     Toggles a humanoid's sprite layer visibility.
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="layer">Layer to toggle visibility for</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetLayerVisibility(EntityUid uid,
        HumanoidVisualLayers layer,
        bool visible,
        bool permanent = false,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
            return;

        var dirty = false;
        SetLayerVisibility(uid, humanoid, layer, visible, permanent, ref dirty);
        if (dirty)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Clones a humanoid's appearance to a target mob, provided they both have humanoid components.
    /// </summary>
    /// <param name="source">Source entity to fetch the original appearance from.</param>
    /// <param name="target">Target entity to apply the source entity's appearance to.</param>
    /// <param name="sourceHumanoid">Source entity's humanoid component.</param>
    /// <param name="targetHumanoid">Target entity's humanoid component.</param>
    public void CloneAppearance(EntityUid source, EntityUid target, HumanoidAppearanceComponent? sourceHumanoid = null,
        HumanoidAppearanceComponent? targetHumanoid = null)
    {
        if (!Resolve(source, ref sourceHumanoid) || !Resolve(target, ref targetHumanoid))
            return;

        targetHumanoid.Species = sourceHumanoid.Species;
        targetHumanoid.SkinColor = sourceHumanoid.SkinColor;
        targetHumanoid.EyeColor = sourceHumanoid.EyeColor;
        targetHumanoid.Age = sourceHumanoid.Age;
        SetSex(target, sourceHumanoid.Sex, false, targetHumanoid);
        SetVoice(target, sourceHumanoid.PreferredVoice, false, targetHumanoid); // TheDen - Add Voice
        targetHumanoid.CustomBaseLayers = new(sourceHumanoid.CustomBaseLayers);
        targetHumanoid.MarkingSet = new(sourceHumanoid.MarkingSet);

        targetHumanoid.Gender = sourceHumanoid.Gender;
        if (TryComp<GrammarComponent>(target, out var grammar))
            grammar.Gender = sourceHumanoid.Gender;

        targetHumanoid.LastProfileLoaded = sourceHumanoid.LastProfileLoaded; // DeltaV - let paradox anomaly be cloned

        Dirty(target, targetHumanoid);
    }

    /// <summary>
    ///     Sets the visibility for multiple layers at once on a humanoid's sprite.
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="layers">An enumerable of all sprite layers that are going to have their visibility set</param>
    /// <param name="visible">The visibility state of the layers given</param>
    /// <param name="permanent">If this is a permanent change, or temporary. Permanent layers are stored in their own hash set.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetLayersVisibility(EntityUid uid, IEnumerable<HumanoidVisualLayers> layers, bool visible, bool permanent = false,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        var dirty = false;

        foreach (var layer in layers)
            SetLayerVisibility(uid, humanoid, layer, visible, permanent, ref dirty);

        if (dirty)
            Dirty(uid, humanoid);
    }

    protected virtual void SetLayerVisibility(
        EntityUid uid,
        HumanoidAppearanceComponent humanoid,
        HumanoidVisualLayers layer,
        bool visible,
        bool permanent,
        ref bool dirty)
    {
        if (visible)
        {
            if (permanent)
                dirty |= humanoid.PermanentlyHidden.Remove(layer);

            dirty |= humanoid.HiddenLayers.Remove(layer);
        }
        else
        {
            if (permanent)
                dirty |= humanoid.PermanentlyHidden.Add(layer);

            dirty |= humanoid.HiddenLayers.Add(layer);
        }
    }

    /// <summary>
    ///     Set a humanoid mob's species. This will change their base sprites, as well as their current
    ///     set of markings to fit against the mob's new species.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="species">The species to set the mob to. Will return if the species prototype was invalid.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetSpecies(EntityUid uid, string species, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || !_proto.TryIndex<SpeciesPrototype>(species, out var prototype))
            return;

        humanoid.Species = species;
        humanoid.MarkingSet.EnsureSpecies(species, humanoid.SkinColor, _markingManager);
        var oldMarkings = humanoid.MarkingSet.GetForwardEnumerator().ToList();
        humanoid.MarkingSet = new(oldMarkings, prototype.MarkingPoints, _markingManager, _proto);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the skin color of this humanoid mob. Will only affect base layers that are not custom,
    ///     custom base layers should use <see cref="SetBaseLayerColor"/> instead.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="skinColor">Skin color to set on the humanoid mob.</param>
    /// <param name="sync">Whether to synchronize this to the humanoid mob, or not.</param>
    /// <param name="verify">Whether to verify the skin color can be set on this humanoid or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public virtual void SetSkinColor(EntityUid uid, Color skinColor, bool sync = true, bool verify = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (!_proto.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
            return;

        if (verify && !SkinColor.VerifySkinColor(species.SkinColoration, skinColor))
            skinColor = SkinColor.ValidSkinTone(species.SkinColoration, skinColor);

        humanoid.SkinColor = skinColor;

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the base layer ID of this humanoid mob. A humanoid mob's 'base layer' is
    ///     the skin sprite that is applied to the mob's sprite upon appearance refresh.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="layer">The layer to target on this humanoid mob.</param>
    /// <param name="id">The ID of the sprite to use. See <see cref="HumanoidSpeciesSpriteLayer"/>.</param>
    /// <param name="sync">Whether to synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetBaseLayerId(EntityUid uid, HumanoidVisualLayers layer, string? id, bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Id = id };
        else
            humanoid.CustomBaseLayers[layer] = new(id);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the color of this humanoid mob's base layer. See <see cref="SetBaseLayerId"/> for a
    ///     description of how base layers work.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="layer">The layer to target on this humanoid mob.</param>
    /// <param name="color">The color to set this base layer to.</param>
    public void SetBaseLayerColor(EntityUid uid, HumanoidVisualLayers layer, Color? color, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Color = color };
        else
            humanoid.CustomBaseLayers[layer] = new(null, color);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set a humanoid mob's sex. This will not change their gender.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="sex">The sex to set the mob to.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetSex(EntityUid uid, Sex sex, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.Sex == sex)
            return;

        humanoid.Sex = sex;
        humanoid.MarkingSet.EnsureSexes(sex, _markingManager);
        if (sync)
            Dirty(uid, humanoid);
    }

    // TheDen - Add Voice
    /// <summary>
    ///     Set a humanoid mob's voice. This will not change their gender.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="voice">The voice to set the mob to.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetVoice(EntityUid uid, Sex voice, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.PreferredVoice == voice)
            return;

        var oldVoice = humanoid.PreferredVoice;
        humanoid.PreferredVoice = voice;
        // humanoid.MarkingSet.EnsureSexes(voice, _markingManager);

        List<Sex> sexes = new();

        if (_proto.TryIndex(humanoid.Species, out var speciesProto))
        {
            foreach (var sex in speciesProto.Sexes)
                sexes.Add(sex);
        }
        else
            sexes.Add(Sex.Unsexed);

        if (!sexes.Contains(voice))
            voice = sexes[0];

        RaiseLocalEvent(uid, new VoiceChangedEvent(oldVoice, voice));

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set the height of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="height">The height to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetHeight(EntityUid uid, float height, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || MathHelper.CloseTo(humanoid.Height, height, 0.001f))
            return;

        var species = _proto.Index(humanoid.Species);
        humanoid.Height = Math.Clamp(height, species.MinHeight, species.MaxHeight);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set the width of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="width">The width to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetWidth(EntityUid uid, float width, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || MathHelper.CloseTo(humanoid.Width, width, 0.001f))
            return;

        var species = _proto.Index(humanoid.Species);
        humanoid.Width = Math.Clamp(width, species.MinWidth, species.MaxWidth);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set the scale of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="scale">The scale to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetScale(EntityUid uid, Vector2 scale, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        var species = _proto.Index(humanoid.Species);

        humanoid.Height = scale.Y;
        humanoid.Width = scale.X;

        if (!HasComp<SpeciesRestrictionExemptComponent>(uid))
        {
            humanoid.Height = Math.Clamp(scale.Y, species.MinHeight, species.MaxHeight);
            humanoid.Width = Math.Clamp(scale.X, species.MinWidth, species.MaxWidth);
        }

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Loads a humanoid character profile directly onto this humanoid mob.
    /// </summary>
    /// <param name="uid">The mob's entity UID.</param>
    /// <param name="profile">The character profile to load.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public virtual void LoadProfile(EntityUid uid, HumanoidCharacterProfile profile, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        SetSpecies(uid, profile.Species, false, humanoid);
        SetSex(uid, profile.Sex, false, humanoid);
        SetVoice(uid, profile.PreferredVoice, false, humanoid); // TheDen - Add Voice
        humanoid.EyeColor = profile.Appearance.EyeColor;
        var ev = new EyeColorInitEvent();
        RaiseLocalEvent(uid, ref ev);

        SetSkinColor(uid, profile.Appearance.SkinColor, false);

        humanoid.MarkingSet.Clear();

        // Add markings that doesn't need coloring. We store them until we add all other markings that doesn't need it.
        var markingFColored = new Dictionary<Marking, MarkingPrototype>();
        foreach (var marking in profile.Appearance.Markings)
        {
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                if (!prototype.ForcedColoring)
                    AddMarking(uid, marking.MarkingId, marking.MarkingColors, false);
                else
                    markingFColored.Add(marking, prototype);
            }
        }

        // Hair/facial hair - this may eventually be deprecated.
        // We need to ensure hair before applying it or coloring can try depend on markings that can be invalid
        var hairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.Hair, out var hairAlpha, _proto)
            ? profile.Appearance.SkinColor.WithAlpha(hairAlpha) : profile.Appearance.HairColor;
        var facialHairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.FacialHair, out var facialHairAlpha, _proto)
            ? profile.Appearance.SkinColor.WithAlpha(facialHairAlpha) : profile.Appearance.FacialHairColor;

        if (_markingManager.Markings.TryGetValue(profile.Appearance.HairStyleId, out var hairPrototype) &&
            _markingManager.CanBeApplied(profile.Species, profile.Sex, hairPrototype, _proto))
            AddMarking(uid, profile.Appearance.HairStyleId, hairColor, false);

        if (_markingManager.Markings.TryGetValue(profile.Appearance.FacialHairStyleId, out var facialHairPrototype) &&
            _markingManager.CanBeApplied(profile.Species, profile.Sex, facialHairPrototype, _proto))
            AddMarking(uid, profile.Appearance.FacialHairStyleId, facialHairColor, false);

        humanoid.MarkingSet.EnsureSpecies(profile.Species, profile.Appearance.SkinColor, _markingManager, _proto);

        // Finally adding marking with forced colors
        foreach (var (marking, prototype) in markingFColored)
        {
            var markingColors = MarkingColoring.GetMarkingLayerColors(
                prototype,
                profile.Appearance.SkinColor,
                profile.Appearance.EyeColor,
                humanoid.MarkingSet
            );
            AddMarking(uid, marking.MarkingId, markingColors, false);
        }

        EnsureDefaultMarkings(uid, humanoid);

        humanoid.Gender = profile.Gender;
        if (TryComp<GrammarComponent>(uid, out var grammar))
            grammar.Gender = profile.Gender;

        humanoid.DisplayPronouns = profile.DisplayPronouns;
        humanoid.StationAiName = profile.StationAiName;
        humanoid.CyborgName = profile.CyborgName;
        humanoid.Age = profile.Age;
        humanoid.Height = profile.Height; // CD - Character Records

        humanoid.CustomSpecieName = profile.Customspeciename;

        var species = _proto.Index(humanoid.Species);

        if (profile.Height <= 0 || profile.Width <= 0)
            SetScale(uid, new Vector2(species.DefaultWidth, species.DefaultHeight), true, humanoid);
        else
            SetScale(uid, new Vector2(profile.Width, profile.Height), true, humanoid);

        _heightAdjust.SetScale(uid, new Vector2(humanoid.Width, humanoid.Height));

        humanoid.LastProfileLoaded = profile; // DeltaV - let paradox anomaly be cloned

        Dirty(uid, humanoid);
        RaiseLocalEvent(uid, new ProfileLoadFinishedEvent());
    }

    /// <summary>
    ///     Adds a marking to this humanoid.
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="marking">Marking ID to use</param>
    /// <param name="color">Color to apply to all marking layers of this marking</param>
    /// <param name="sync">Whether to immediately sync this marking or not</param>
    /// <param name="forced">If this marking was forced (ignores marking points)</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void AddMarking(EntityUid uid, string marking, Color? color = null, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid)
            || !_markingManager.Markings.TryGetValue(marking, out var prototype))
            return;

        var markingObject = prototype.AsMarking();
        markingObject.Forced = forced;
        if (color != null)
        {
            for (var i = 0; i < prototype.Sprites.Count; i++)
            {
                markingObject.SetColor(i, color.Value);
            }
        }

        humanoid.MarkingSet.AddBack(prototype.MarkingCategory, markingObject);

        if (sync)
            Dirty(uid, humanoid);
    }

    private void EnsureDefaultMarkings(EntityUid uid, HumanoidAppearanceComponent? humanoid)
    {
        if (!Resolve(uid, ref humanoid))
            return;
        humanoid.MarkingSet.EnsureDefault(humanoid.SkinColor, humanoid.EyeColor, _markingManager);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="marking">Marking ID to use</param>
    /// <param name="colors">Colors to apply against this marking's set of sprites.</param>
    /// <param name="sync">Whether to immediately sync this marking or not</param>
    /// <param name="forced">If this marking was forced (ignores marking points)</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void AddMarking(EntityUid uid, string marking, IReadOnlyList<Color> colors, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid)
            || !_markingManager.Markings.TryGetValue(marking, out var prototype))
            return;

        var markingObject = new Marking(marking, colors);
        markingObject.Forced = forced;
        humanoid.MarkingSet.AddBack(prototype.MarkingCategory, markingObject);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    /// Takes ID of the species prototype, returns UI-friendly name of the species.
    /// </summary>
    public string GetSpeciesRepresentation(string speciesId, string? customespeciename)
    {
        if (!string.IsNullOrEmpty(customespeciename))
            return Loc.GetString(customespeciename);

        if (_proto.TryIndex<SpeciesPrototype>(speciesId, out var species))
            return Loc.GetString(species.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    public string GetAgeRepresentation(string species, int age)
    {
        if (!_proto.TryIndex<SpeciesPrototype>(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
            return Loc.GetString("identity-age-young");

        if (age < speciesPrototype.OldAge)
            return Loc.GetString("identity-age-middle-aged");

        return Loc.GetString("identity-age-old");
    }

    // Floofstation section
    public void SetMarkingVisibility(
        EntityUid uid,
        HumanoidAppearanceComponent? humanoid,
        string markingId,
        bool visible)
    {
        if (!_markingManager.Markings.TryGetValue(markingId, out var prototype))
            return;
        if (!Resolve(uid, ref humanoid))
            return;

        if (visible)
            humanoid.HiddenMarkings.Remove(markingId);
        else
            humanoid.HiddenMarkings.Add(markingId);

        Dirty(uid, humanoid);
    }
    // Floofstation section end
}
