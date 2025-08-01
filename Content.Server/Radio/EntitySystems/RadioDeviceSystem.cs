// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 20kdc <asdd2808@gmail.com>
// SPDX-FileCopyrightText: 2023 Slava0135 <40753025+Slava0135@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 FoxxoTrystan <45297731+FoxxoTrystan@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 beck-thompson <107373427+beck-thompson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 sleepyyapril <flyingkarii@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Server.Language;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Radio;
using Content.Shared.Chat;
using Content.Shared.Radio.Components;
using Content.Shared.UserInterface; // Nuclear-14
using Content.Shared._NC.Radio;
using Content.Shared.Language.Components; // Nuclear-14
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player; // Nuclear-14
using Robust.Shared.Prototypes;

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles radio speakers and microphones (which together form a hand-held radio).
/// </summary>
public sealed class RadioDeviceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    // Used to prevent a shitter from using a bunch of radios to spam chat.
    private HashSet<(string, EntityUid)> _recentlySent = new();

    // Frontier: minimum, maximum radio frequencies
    private const int MinRadioFrequency = 1000;
    private const int MaxRadioFrequency = 3000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioMicrophoneComponent, ComponentInit>(OnMicrophoneInit);
        SubscribeLocalEvent<RadioMicrophoneComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RadioMicrophoneComponent, ActivateInWorldEvent>(OnActivateMicrophone);
        SubscribeLocalEvent<RadioMicrophoneComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<RadioMicrophoneComponent, ListenAttemptEvent>(OnAttemptListen);
        SubscribeLocalEvent<RadioMicrophoneComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<RadioSpeakerComponent, ComponentInit>(OnSpeakerInit);
        SubscribeLocalEvent<RadioSpeakerComponent, ActivateInWorldEvent>(OnActivateSpeaker);
        SubscribeLocalEvent<RadioSpeakerComponent, RadioReceiveEvent>(OnReceiveRadio);

        SubscribeLocalEvent<IntercomComponent, EncryptionChannelsChangedEvent>(OnIntercomEncryptionChannelsChanged);
        SubscribeLocalEvent<IntercomComponent, ToggleIntercomMicMessage>(OnToggleIntercomMic);
        SubscribeLocalEvent<IntercomComponent, ToggleIntercomSpeakerMessage>(OnToggleIntercomSpeaker);
        SubscribeLocalEvent<IntercomComponent, SelectIntercomChannelMessage>(OnSelectIntercomChannel);

        SubscribeLocalEvent<RadioMicrophoneComponent, BeforeActivatableUIOpenEvent>(OnBeforeHandheldRadioUiOpen);
        SubscribeLocalEvent<RadioMicrophoneComponent, ToggleHandheldRadioMicMessage>(OnToggleHandheldRadioMic);
        SubscribeLocalEvent<RadioMicrophoneComponent, ToggleHandheldRadioSpeakerMessage>(OnToggleHandheldRadioSpeaker);
        SubscribeLocalEvent<RadioMicrophoneComponent, SelectHandheldRadioFrequencyMessage>(OnChangeHandheldRadioFrequency);

        SubscribeLocalEvent<IntercomComponent, MapInitEvent>(OnMapInit); // Frontier
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _recentlySent.Clear();
    }


    #region Component Init
    private void OnMicrophoneInit(EntityUid uid, RadioMicrophoneComponent component, ComponentInit args)
    {
        if (component.Enabled)
            EnsureComp<ActiveListenerComponent>(uid).Range = component.ListenRange;
        else
            RemCompDeferred<ActiveListenerComponent>(uid);
    }

    private void OnSpeakerInit(EntityUid uid, RadioSpeakerComponent component, ComponentInit args)
    {
        if (component.Enabled)
            EnsureComp<ActiveRadioComponent>(uid).Channels.UnionWith(component.Channels);
        else
            RemCompDeferred<ActiveRadioComponent>(uid);
    }
    #endregion

    #region Toggling
    private void OnActivateMicrophone(EntityUid uid, RadioMicrophoneComponent component, ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        if (!component.ToggleOnInteract)
            return;

        ToggleRadioMicrophone(uid, args.User, args.Handled, component);
        args.Handled = true;
    }

    private void OnActivateSpeaker(EntityUid uid, RadioSpeakerComponent component, ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        if (!component.ToggleOnInteract)
            return;

        ToggleRadioSpeaker(uid, args.User, args.Handled, component);
        args.Handled = true;
    }

    public void ToggleRadioMicrophone(EntityUid uid, EntityUid user, bool quiet = false, RadioMicrophoneComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetMicrophoneEnabled(uid, user, !component.Enabled, quiet, component);
    }

    private void OnPowerChanged(EntityUid uid, RadioMicrophoneComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;
        SetMicrophoneEnabled(uid, null, false,  true, component);
    }

    public void SetMicrophoneEnabled(EntityUid uid, EntityUid? user, bool enabled, bool quiet = false, RadioMicrophoneComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (component.PowerRequired && !this.IsPowered(uid, EntityManager))
            return;

        component.Enabled = enabled;

        if (!quiet && user != null)
        {
            var state = Loc.GetString(component.Enabled ? "handheld-radio-component-on-state" : "handheld-radio-component-off-state");
            var message = Loc.GetString("handheld-radio-component-on-use", ("radioState", state));
            _popup.PopupEntity(message, user.Value, user.Value);
        }

        _appearance.SetData(uid, RadioDeviceVisuals.Broadcasting, component.Enabled);
        if (component.Enabled)
            EnsureComp<ActiveListenerComponent>(uid).Range = component.ListenRange;
        else
            RemCompDeferred<ActiveListenerComponent>(uid);
    }

    public void ToggleRadioSpeaker(EntityUid uid, EntityUid user, bool quiet = false, RadioSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetSpeakerEnabled(uid, user, !component.Enabled, quiet, component);
    }

    public void SetSpeakerEnabled(EntityUid uid, EntityUid? user, bool enabled, bool quiet = false, RadioSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Enabled = enabled;

        if (!quiet && user != null)
        {
            var state = Loc.GetString(component.Enabled ? "handheld-radio-component-on-state" : "handheld-radio-component-off-state");
            var message = Loc.GetString("handheld-radio-component-on-use", ("radioState", state));
            _popup.PopupEntity(message, user.Value, user.Value);
        }

        _appearance.SetData(uid, RadioDeviceVisuals.Speaker, component.Enabled);
        if (component.Enabled)
            EnsureComp<ActiveRadioComponent>(uid).Channels.UnionWith(component.Channels);
        else
            RemCompDeferred<ActiveRadioComponent>(uid);
    }
    #endregion

    private void OnExamine(EntityUid uid, RadioMicrophoneComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var proto = _protoMan.Index<RadioChannelPrototype>(component.BroadcastChannel);

        using (args.PushGroup(nameof(RadioMicrophoneComponent)))
        {
            args.PushMarkup(Loc.GetString(
                "handheld-radio-component-on-examine",
                ("frequency", component.Frequency),
                ("color", proto.Color.ToHex())));
            args.PushMarkup(Loc.GetString(
                "handheld-radio-component-channel-examine",
                ("channel", proto.LocalizedName),
                ("color", proto.Color.ToHex())));
        }
    }

    private void OnListen(EntityUid uid, RadioMicrophoneComponent component, ListenEvent args)
    {
        if (HasComp<RadioSpeakerComponent>(args.Source))
            return; // no feedback loops please.

        if (_recentlySent.Add((args.Message, args.Source)))
            _radio.SendRadioMessage(args.Source, args.Message, _protoMan.Index<RadioChannelPrototype>(component.BroadcastChannel), uid, /*Nuclear-14-start*/ frequency: component.Frequency /*Nuclear-14-end*/);
    }

    private void OnAttemptListen(EntityUid uid, RadioMicrophoneComponent component, ListenAttemptEvent args)
    {
        if (component.PowerRequired && !this.IsPowered(uid, EntityManager)
            || component.UnobstructedRequired && !_interaction.InRangeUnobstructed(args.Source, uid, 0))
            args.Cancel();
    }

    private void OnReceiveRadio(EntityUid uid, RadioSpeakerComponent component, ref RadioReceiveEvent args)
    {
        var parent = Transform(uid).ParentUid;

        if (!TryComp(parent, out ActorComponent? actor))
            return;

        var hasSpeakerComponent = TryComp<LanguageSpeakerComponent>(args.MessageSource, out var languageSpeakerComponent);
        var canUnderstand = _language.CanUnderstand(
            parent,
            args.Language.ID,
            hasSpeakerComponent ? (args.MessageSource, languageSpeakerComponent) : null);

        var msg = new MsgChatMessage
        {
            Message = canUnderstand ? args.OriginalChatMsg : args.LanguageObfuscatedChatMsg
        };
        _netMan.ServerSendMessage(msg, actor.PlayerSession.Channel);
    }

    private void OnIntercomEncryptionChannelsChanged(Entity<IntercomComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        ent.Comp.SupportedChannels = args.Component.Channels.Select(p => new ProtoId<RadioChannelPrototype>(p)).ToList();

        var channel = args.Component.DefaultChannel;
        if (ent.Comp.CurrentChannel != null && ent.Comp.SupportedChannels.Contains(ent.Comp.CurrentChannel.Value))
            channel = ent.Comp.CurrentChannel;

        SetIntercomChannel(ent, channel);
    }

    private void OnToggleIntercomMic(Entity<IntercomComponent> ent, ref ToggleIntercomMicMessage args)
    {
        if (ent.Comp.RequiresPower && !this.IsPowered(ent, EntityManager))
            return;

        SetMicrophoneEnabled(ent, args.Actor, args.Enabled, true);
        ent.Comp.MicrophoneEnabled = args.Enabled;
        Dirty(ent);
    }

    private void OnToggleIntercomSpeaker(Entity<IntercomComponent> ent, ref ToggleIntercomSpeakerMessage args)
    {
        if (ent.Comp.RequiresPower && !this.IsPowered(ent, EntityManager))
            return;

        SetSpeakerEnabled(ent, args.Actor, args.Enabled, true);
        ent.Comp.SpeakerEnabled = args.Enabled;
        Dirty(ent);
    }

    private void OnSelectIntercomChannel(Entity<IntercomComponent> ent, ref SelectIntercomChannelMessage args)
    {
        if (ent.Comp.RequiresPower && !this.IsPowered(ent, EntityManager))
            return;

        if (!_protoMan.TryIndex<RadioChannelPrototype>(args.Channel, out var channel) || !ent.Comp.SupportedChannels.Contains(args.Channel)) // Nuclear-14: add channel
            return;

        SetIntercomChannel(ent, args.Channel);
    }

    private void SetIntercomChannel(Entity<IntercomComponent> ent, ProtoId<RadioChannelPrototype>? channel)
    {
        ent.Comp.CurrentChannel = channel;

        if (channel == null)
        {
            SetSpeakerEnabled(ent, null, false);
            SetMicrophoneEnabled(ent, null, false);
            ent.Comp.MicrophoneEnabled = false;
            ent.Comp.SpeakerEnabled = false;
            Dirty(ent);
            return;
        }

        if (TryComp<RadioMicrophoneComponent>(ent, out var mic))
        {
            mic.BroadcastChannel = channel;
            if(_protoMan.TryIndex<RadioChannelPrototype>(channel, out var channelProto)) // Frontier
                mic.Frequency = _radio.GetFrequency(ent, channelProto); // Frontier
        }
        if (TryComp<RadioSpeakerComponent>(ent, out var speaker))
            speaker.Channels = new(){ channel };
        Dirty(ent);
    }

    // Nuclear-14-Start
    #region Handheld Radio

    private void OnBeforeHandheldRadioUiOpen(Entity<RadioMicrophoneComponent> microphone, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateHandheldRadioUi(microphone);
    }

    private void OnToggleHandheldRadioMic(Entity<RadioMicrophoneComponent> microphone, ref ToggleHandheldRadioMicMessage args)
    {
        if (!args.Actor.Valid)
            return;

        SetMicrophoneEnabled(microphone, args.Actor, args.Enabled, true);
        UpdateHandheldRadioUi(microphone);
    }

    private void OnToggleHandheldRadioSpeaker(Entity<RadioMicrophoneComponent> microphone, ref ToggleHandheldRadioSpeakerMessage args)
    {
        if (!args.Actor.Valid)
            return;

        SetSpeakerEnabled(microphone, args.Actor, args.Enabled, true);
        UpdateHandheldRadioUi(microphone);
    }

    private void OnChangeHandheldRadioFrequency(Entity<RadioMicrophoneComponent> microphone, ref SelectHandheldRadioFrequencyMessage args)
    {
        if (!args.Actor.Valid)
            return;

        // Update frequency if valid and within range.
        if (args.Frequency >= MinRadioFrequency && args.Frequency <= MaxRadioFrequency)
            microphone.Comp.Frequency = args.Frequency;
        // Update UI with current frequency.
        UpdateHandheldRadioUi(microphone);
    }

    private void UpdateHandheldRadioUi(Entity<RadioMicrophoneComponent> radio)
    {
        var speakerComp = CompOrNull<RadioSpeakerComponent>(radio);
        var frequency = radio.Comp.Frequency;

        var micEnabled = radio.Comp.Enabled;
        var speakerEnabled = speakerComp?.Enabled ?? false;
        var state = new HandheldRadioBoundUIState(micEnabled, speakerEnabled, frequency);
        if (TryComp<UserInterfaceComponent>(radio, out var uiComp))
            _ui.SetUiState((radio.Owner, uiComp), HandheldRadioUiKey.Key, state); // Frontier: TrySetUiState<SetUiState
    }

    #endregion
    // Nuclear-14-End

    // Frontier: init intercom with map
    private void OnMapInit(EntityUid uid, IntercomComponent ent, MapInitEvent args)
    {
        // Set initial frequency (must be done regardless of power/enabled)
        if (ent.CurrentChannel != null &&
                _protoMan.TryIndex(ent.CurrentChannel, out var channel) &&
                TryComp(uid, out RadioMicrophoneComponent? mic))
        {
            mic.Frequency = channel.Frequency;
        }
    }
    // End Frontier
}
