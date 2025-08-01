// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Weather;
using Robust.Shared.Console;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Weather;

public sealed class WeatherSystem : SharedWeatherSystem
{
    [Dependency] private readonly IConsoleHost _console = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeatherComponent, ComponentGetState>(OnWeatherGetState);
        _console.RegisterCommand("weather",
            Loc.GetString("cmd-weather-desc"),
            Loc.GetString("cmd-weather-help"),
            WeatherTwo,
            WeatherCompletion);
    }

    private void OnWeatherGetState(EntityUid uid, WeatherComponent component, ref ComponentGetState args)
    {
        args.State = new WeatherComponentState(component.Weather);
    }

    [AdminCommand(AdminFlags.Fun)]
    private void WeatherTwo(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError($"A");
            return;
        }

        if (!int.TryParse(args[0], out var mapInt))
        {
            return;
        }

        var mapId = new MapId(mapInt);

        if (!MapManager.MapExists(mapId))
        {
            return;
        }

        TimeSpan? endTime = null;

        if (args.Length == 3)
        {
            if (int.TryParse(args[2], out var durationInt))
            {
                var curTime = Timing.CurTime;
                var maxTime = TimeSpan.MaxValue;

                // If it's already running then just fade out with how much time we're into the weather.
                if (TryComp<WeatherComponent>(MapManager.GetMapEntityId(mapId), out var weatherComp) &&
                    weatherComp.Weather.TryGetValue(args[1], out var existing))
                {
                    maxTime = curTime - existing.StartTime;
                }

                endTime = curTime + TimeSpan.FromSeconds(durationInt);

                if (endTime > maxTime)
                    endTime = maxTime;
            }
        }

        if (args[1].Equals("null"))
        {
            SetWeather(mapId, null, endTime);
        }
        else if (ProtoMan.TryIndex<WeatherPrototype>(args[1], out var weatherProto))
        {
            SetWeather(mapId, weatherProto, endTime);
        }
        else
        {
            shell.WriteError($"Unable to parse weather prototype");
        }
    }

    private CompletionResult WeatherCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.MapIds(EntityManager), "Map Id");

        var a = CompletionHelper.PrototypeIDs<WeatherPrototype>(true, ProtoMan);
        return CompletionResult.FromHintOptions(a, Loc.GetString("cmd-weather-hint"));
    }
}
