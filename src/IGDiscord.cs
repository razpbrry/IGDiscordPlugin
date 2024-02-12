﻿using IGDiscord.Models.MessageInfo;
using IGDiscord.Models;
using IGDiscord.Utils;
using IGDiscord.Services.Interfaces;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;
using IGDiscord.Models.Discord;

namespace IGDiscord;

public partial class IGDiscord : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "IGDiscord";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "raz";
    public override string ModuleDescription => "A Discord webhook plugin for Imperfect Gamers";

    public Config Config { get; set; }

    public Config? _config;
    public string ConfigPath;
    public StatusData StatusData = new();
    private readonly IConfigService _configService;
    private readonly IDiscordService _discordService;
    private readonly ILogger<IGDiscord> _logger;

    public IGDiscord(
        IConfigService configService,
        IDiscordService discordService,
        ILogger<IGDiscord> logger)
    {
        _configService = configService;
        _discordService = discordService;
        _logger = logger;
    }

    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);
    }

    public override void Load(bool hotReload)
    {
        if (Config != null)
        {

            WebhookMessage initialWebhookMessage = _discordService.CreateWebhookMessage(Config.StatusMessageInfo, StatusData);

            if (string.IsNullOrEmpty(Config.StatusMessageInfo.MessageId))
            {
                // Send initial message
                Task.Run(async () =>
                {
                    UpdateStatusData();

                    var messageId = await _discordService.SendInitialStatusMessage(Config.StatusMessageInfo, initialWebhookMessage);

                    if (messageId != null)
                    {
                        Config.StatusMessageInfo.MessageId = messageId;

                        _configService.UpdateConfig(Config, ConfigPath);
                    }
                    else
                    {
                        Util.PrintError("Something went wrong getting a reponse when sending message.");
                    }
                });
            }
            else
            {
                // Message exists, update the message
                Task.Run(async () =>
                {
                    var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(Config.StatusMessageInfo.MessageInterval));
                    while (await periodicTimer.WaitForNextTickAsync())
                    {
                        Util.PrintLog("Updating status message");
                        Util.PrintLog($"MessageId: {Config.StatusMessageInfo.MessageId}");
                        Util.PrintLog($"WebhookUri: {Config.StatusMessageInfo.WebhookUri}");

                        UpdateStatusData();

                        WebhookMessage updatedWebhookMessage = _discordService.UpdateWebhookMessage(initialWebhookMessage, StatusData);

                        await _discordService.UpdateStatusMessage(Config.StatusMessageInfo, updatedWebhookMessage);
                    }
                });
            }
        }
        else
        {
            _logger.LogInformation("The config file did not load correctly. Please check that there is a config.json file in the plugin directory.");
        };
    }

    private void UpdateStatusData()
    {
        Server.NextFrame(() =>
        {
            StatusData.MapName = NativeAPI.GetMapName();
            StatusData.Timestamp = DateTime.Now;
        });
    }

    public void OnConfigParsed(Config config)
    {
        if (config.StatusMessageInfo == null)
        {
            config.StatusMessageInfo = new StatusMessageInfo()
            {
                MessageType = Constants.MessageType.ServerStatus,
                WebhookUri = "https://discord.com/api/webhooks/###############/#################",
                MessageInterval = 300
            };
        }

        ConfigPath = _configService.GetConfigPath(ModuleDirectory, ModuleName);

        Config = config;
    }
}