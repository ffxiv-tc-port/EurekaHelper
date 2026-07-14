using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions.Generated;

namespace EurekaHelper;

internal static class Loc
{
	internal static class Ui
	{
		public const string TimeAgo = "TimeAgo";

		public const string BossnameReplacedWithFateBossName = "BossnameReplacedWithFateBossName";

		public const string BossshortnameReplacedWithFateBossShortName = "BossshortnameReplacedWithFateBossShortName";

		public const string FatenameReplacedWithFateName = "FatenameReplacedWithFateName";

		public const string FlagReplacedWithFlag = "FlagReplacedWithFlag";

		public const string Day = "Day";

		public const string Night = "Night";

		public const string AvailableFormattings = "AvailableFormattings";

		public const string DisclaimerReadThis = "DisclaimerReadThis";

		public const string ArisuDisplayNextWeatherForCrabCassieSkoll = "ArisuDisplayNextWeatherForCrabCassieSkoll";

		public const string EalarmsOpensClosesTheEurekaAlarmsWindow = "EalarmsOpensClosesTheEurekaAlarmsWindow";

		public const string ErelicOpensClosesTheEurekaRelicHelperWindow = "ErelicOpensClosesTheEurekaRelicHelperWindow";

		public const string EtrackersAttemptsToGetATrackerForTheCurrentInstanceInTheSameDatacenter = "EtrackersAttemptsToGetATrackerForTheCurrentInstanceInTheSameDatacenter";

		public const string EurekahelperEhEhelperOpensClosesTheConfigurationWindow = "EurekahelperEhEhelperOpensClosesTheConfigurationWindow";

		public const string ClickTo0 = "ClickTo0";

		public const string Key0123Ago = "Key0123Ago";

		public const string Key0In1 = "Key0In1";

		public const string Key0ServerId1 = "Key0ServerId1";

		public const string Key0WeatherIsUpNowItEndsIn = "Key0WeatherIsUpNowItEndsIn";

		public const string HasBeenPoppedAndIsOnARespawnTimer = "HasBeenPoppedAndIsOnARespawnTimer";

		public const string OneOfTheRequirementsIsNotMetToSpawnPrepTheNm = "OneOfTheRequirementsIsNotMetToSpawnPrepTheNm";

		public const string ReadyToBeSpawned = "ReadyToBeSpawned";

		public const string ASoundQueWillBePlayedWheneverAnNmPops = "ASoundQueWillBePlayedWheneverAnNmPops";

		public const string AboutTab = "AboutTab";

		public const string AboutSection = "AboutSection";

		public const string AddButton = "AddButton";

		public const string AddAlarm = "AddAlarm";

		public const string AddAnAlarm = "AddAnAlarm";

		public const string AddKnownElementalMapMarkers = "AddKnownElementalMapMarkers";

		public const string AddKnownElementalMapMarkersTooltip = "AddKnownElementalMapMarkersTooltip";

		public const string AlarmConfigurationsEdit = "AlarmConfigurationsEdit";

		public const string AlarmInformation = "AlarmInformation";

		public const string AlarmName = "AlarmName";

		public const string AlwaysClearElementalsSetting = "AlwaysClearElementalsSetting";

		public const string AlwaysClearTheElementalListWheneverYouJoinAEurekaZone = "AlwaysClearTheElementalListWheneverYouJoinAEurekaZone";

		public const string AnemosTab = "AnemosTab";

		public const string AssistToCrowdsourceForElementalLocations = "AssistToCrowdsourceForElementalLocations";

		public const string AttemptsToAutoPopFateWhenConnectedToATrackerIfYouHaveThePassword = "AttemptsToAutoPopFateWhenConnectedToATrackerIfYouHaveThePassword";

		public const string AttemptsToGetATrackerForTheCurrentInstanceInTheSameDatacenter = "AttemptsToGetATrackerForTheCurrentInstanceInTheSameDatacenter";

		public const string AutoCreateTrackerSetting = "AutoCreateTrackerSetting";

		public const string AutoCreatesTrackerWhenJoiningAnInstanceAndPrintsTheTrackerLinkToChat = "AutoCreatesTrackerWhenJoiningAnInstanceAndPrintsTheTrackerLinkToChat";

		public const string AutoMarkElementalsSetting = "AutoMarkElementalsSetting";

		public const string AutoMarkElementalsTooltip = "AutoMarkElementalsTooltip";

		public const string AutoPopFateSetting = "AutoPopFateSetting";

		public const string AutoPopFateWithinRangeSetting = "AutoPopFateWithinRangeSetting";

		public const string Bedo9041ForEurekaplugin = "Bedo9041ForEurekaplugin";

		public const string Blizzards = "Blizzards";

		public const string BunnySoundEffectSetting = "BunnySoundEffectSetting";

		public const string ButtonColumn = "ButtonColumn";

		public const string ChatChannelsSetting = "ChatChannelsSetting";

		public const string ChatSoundEffectToBePlayedWhenAnNmPops = "ChatSoundEffectToBePlayedWhenAnNmPops";

		public const string ClearAllElementals = "ClearAllElementals";

		public const string ClearAllMapMarkers = "ClearAllMapMarkers";

		public const string CodeColumn = "CodeColumn";

		public const string CodeLabel = "CodeLabel";

		public const string CommandsSection = "CommandsSection";

		public const string ConfigurationTab = "ConfigurationTab";

		public const string Confirm = "Confirm";

		public const string ContactSection = "ContactSection";

		public const string CopyAction = "CopyAction";

		public const string CopyTrackerLinkToClipboard = "CopyTrackerLinkToClipboard";

		public const string CopyTrackerPasswordToClipboard = "CopyTrackerPasswordToClipboard";

		public const string CreateANewTracker = "CreateANewTracker";

		public const string CreateAnemosTracker = "CreateAnemosTracker";

		public const string CreateHydatosTracker = "CreateHydatosTracker";

		public const string CreatePagosTracker = "CreatePagosTracker";

		public const string CreatePyrosTracker = "CreatePyrosTracker";

		public const string CreditsSection = "CreditsSection";

		public const string CrowdsourceLocationsSetting = "CrowdsourceLocationsSetting";

		public const string CustomMessages = "CustomMessages";

		public const string DayIn0 = "DayIn0";

		public const string DeleteColumn = "DeleteColumn";

		public const string DeleteAll = "DeleteAll";

		public const string DeleteTheCurrentAlarm = "DeleteTheCurrentAlarm";

		public const string DisplayAToastWheneverTheAlarmIsTriggered = "DisplayAToastWheneverTheAlarmIsTriggered";

		public const string DisplayElementalSetting = "DisplayElementalSetting";

		public const string DisplayElementalToastSetting = "DisplayElementalToastSetting";

		public const string DisplayFateProgressSetting = "DisplayFateProgressSetting";

		public const string DisplayNextWeatherForCrabCassieSkoll = "DisplayNextWeatherForCrabCassieSkoll";

		public const string DisplayNmPopSetting = "DisplayNmPopSetting";

		public const string DisplayServerIdInChatSetting = "DisplayServerIdInChatSetting";

		public const string DisplayToastSetting = "DisplayToastSetting";

		public const string DisplaysAToastWheneverAnElementalAppearsNearThePlayer = "DisplaysAToastWheneverAnElementalAppearsNearThePlayer";

		public const string DisplaysAToastWheneverAnNmPops = "DisplaysAToastWheneverAnNmPops";

		public const string DisplaysInChatWheneverAnElementalAppearsNearThePlayer = "DisplaysInChatWheneverAnElementalAppearsNearThePlayer";

		public const string DisplaysTheNmThatPoppedInChat = "DisplaysTheNmThatPoppedInChat";

		public const string DoneColumn = "DoneColumn";

		public const string ET = "ET";

		public const string Earth = "Earth";

		public const string EditAlarm = "EditAlarm";

		public const string EditTheCurrentAlarm = "EditTheCurrentAlarm";

		public const string Electr0sheepForEurekatrackerautopopper = "Electr0sheepForEurekatrackerautopopper";

		public const string Element = "Element";

		public const string ElementalColumn = "ElementalColumn";

		public const string ElementalFoundThatIsNotInThePluginDatabase = "ElementalFoundThatIsNotInThePluginDatabase";

		public const string ElementalPayloadCopyTooltip = "ElementalPayloadCopyTooltip";

		public const string ElementalPayloadOptionsTooltip = "ElementalPayloadOptionsTooltip";

		public const string ElementalPayloadShoutTooltip = "ElementalPayloadShoutTooltip";

		public const string ElementalsTab = "ElementalsTab";

		public const string EnableBunnyFatesSetting = "EnableBunnyFatesSetting";

		public const string EnableDisplayForBunnyFates = "EnableDisplayForBunnyFates";

		public const string EnableNmPopSoundSetting = "EnableNmPopSoundSetting";

		public const string EndLabel = "EndLabel";

		public const string EndsIn0 = "EndsIn0";

		public const string Enter6DigitCode = "Enter6DigitCode";

		public const string EnterTrackerPassword = "EnterTrackerPassword";

		public const string EurekaHelper = "EurekaHelper";

		public const string EurekaHelperAlarms = "EurekaHelperAlarms";

		public const string EurekaHelperRelic = "EurekaHelperRelic";

		public const string ExportsTheCurrentTrackerToANewOne = "ExportsTheCurrentTrackerToANewOne";

		public const string FailedToGetValueForSomeReasonPleaseContactAuthor = "FailedToGetValueForSomeReasonPleaseContactAuthor";

		public const string FairSkies = "FairSkies";

		public const string FateLevel0 = "FateLevel0";

		public const string FateName0 = "FateName0";

		public const string FfxivDevCommunity = "FfxivDevCommunity";

		public const string Fire = "Fire";

		public const string Fog = "Fog";

		public const string Found = "Found";

		public const string Gales = "Gales";

		public const string GitHubLabel = "GitHubLabel";

		public const string Gloom = "Gloom";

		public const string GreenLegend = "GreenLegend";

		public const string HeatWaves = "HeatWaves";

		public const string HydatosTab = "HydatosTab";

		public const string Ice = "Ice";

		public const string Id0TTviewers1 = "Id0TTviewers1";

		public const string In = "In";

		public const string In0 = "In0";

		public const string InformationSection = "InformationSection";

		public const string InstanceTab = "InstanceTab";

		public const string InvalidTracker = "InvalidTracker";

		public const string InventoriesLabel = "InventoriesLabel";

		public const string IsAt = "IsAt";

		public const string IssuesFeedbackLabel = "IssuesFeedbackLabel";

		public const string ItemColumn = "ItemColumn";

		public const string JobCategoryColumn = "JobCategoryColumn";

		public const string JoinsATrackerWithTheSpecifiedIdAndPassword = "JoinsATrackerWithTheSpecifiedIdAndPassword";

		public const string KangaszForEurekahelperContributions = "KangaszForEurekahelperContributions";

		public const string LastSeen = "LastSeen";

		public const string LeaveTheCurrentTracker = "LeaveTheCurrentTracker";

		public const string Lightning = "Lightning";

		public const string LinkedItem = "LinkedItem";

		public const string LocationColumn = "LocationColumn";

		public const string LevelColumn = "LevelColumn";

		public const string MarkColumn = "MarkColumn";

		public const string MinutesBefore = "MinutesBefore";

		public const string AlarmNameHint = "AlarmNameHint";

		public const string NameLabel = "NameLabel";

		public const string Next0Weather1In = "Next0Weather1In";

		public const string NightIn0 = "NightIn0";

		public const string NightRequired = "NightRequired";

		public const string NmColumn = "NmColumn";

		public const string NmSoundEffectSetting = "NmSoundEffectSetting";

		public const string Nm0 = "Nm0";

		public const string WeatherNone = "WeatherNone";

		public const string NotConnectedToATracker = "NotConnectedToATracker";

		public const string Note = "Note";

		public const string OpensClosesTheConfigurationWindow = "OpensClosesTheConfigurationWindow";

		public const string OpensClosesTheEurekaAlarmsWindow = "OpensClosesTheEurekaAlarmsWindow";

		public const string OpensClosesTheEurekaRelicHelperWindow = "OpensClosesTheEurekaRelicHelperWindow";

		public const string OpensTheTrackerInABrowser = "OpensTheTrackerInABrowser";

		public const string OrangeLegend = "OrangeLegend";

		public const string PagosTab = "PagosTab";

		public const string PasswordColumn = "PasswordColumn";

		public const string PasswordLabel = "PasswordLabel";

		public const string Password0 = "Password0";

		public const string PayloadOptionsSetting = "PayloadOptionsSetting";

		public const string PayloadOptionsTooltip = "PayloadOptionsTooltip";

		public const string PopButton = "PopButton";

		public const string PoppedAt = "PoppedAt";

		public const string PoppedOn0LocalTime = "PoppedOn0LocalTime";

		public const string PrintsAMessageWheneverTheAlarmIsTriggered = "PrintsAMessageWheneverTheAlarmIsTriggered";

		public const string PrintsTheNmProgressInChat = "PrintsTheNmProgressInChat";

		public const string PublicTrackers = "PublicTrackers";

		public const string PyrosTab = "PyrosTab";

		public const string RandomizeMapCoords = "RandomizeMapCoords";

		public const string RandomizesMapCoordsToRangeOf05RecommendedToEnable = "RandomizesMapCoordsToRangeOf05RecommendedToEnable";

		public const string ReadyState = "ReadyState";

		public const string RedLegend = "RedLegend";

		public const string RequirementsLabel = "RequirementsLabel";

		public const string ResetButton = "ResetButton";

		public const string ResetAll = "ResetAll";

		public const string RespawnIn = "RespawnIn";

		public const string RespawnTimeMayBeWrongDueToOtherConditions = "RespawnTimeMayBeWrongDueToOtherConditions";

		public const string RetainersLabel = "RetainersLabel";

		public const string SC = "SC";

		public const string SaddlebagsLabel = "SaddlebagsLabel";

		public const string ServerId0 = "ServerId0";

		public const string SetButton = "SetButton";

		public const string SetTheChannelWhichThePluginMessagesWillDisplayDefaultEcho = "SetTheChannelWhichThePluginMessagesWillDisplayDefaultEcho";

		public const string SetTrackerToPrivate = "SetTrackerToPrivate";

		public const string SetTrackerToPublic = "SetTrackerToPublic";

		public const string SettingsLabel = "SettingsLabel";

		public const string ShoutAction = "ShoutAction";

		public const string ShowLevelOnTrackerSetting = "ShowLevelOnTrackerSetting";

		public const string Showers = "Showers";

		public const string Snow = "Snow";

		public const string SomethingWentWrongPleaseContactTheAuthorN0 = "SomethingWentWrongPleaseContactTheAuthorN0";

		public const string SoundEffectToBePlayedWhenAnNmPops = "SoundEffectToBePlayedWhenAnNmPops";

		public const string SoundEffectToBePlayedWhenBunnySpawns = "SoundEffectToBePlayedWhenBunnySpawns";

		public const string SoundEffectLabel = "SoundEffectLabel";

		public const string SpawnedBy = "SpawnedBy";

		public const string StartLabel = "StartLabel";

		public const string SuccessfullyCreatedATracker0 = "SuccessfullyCreatedATracker0";

		public const string SuccessfullyExportedThePreviousTracker0 = "SuccessfullyExportedThePreviousTracker0";

		public const string ThisDatacenterIsNotSupportedCurrentlyPleaseSubmitAnIssueIfYouThinkThisIsIncorrect = "ThisDatacenterIsNotSupportedCurrentlyPleaseSubmitAnIssueIfYouThinkThisIsIncorrect";

		public const string Thunder = "Thunder";

		public const string Thunderstorms = "Thunderstorms";

		public const string TimeLabel = "TimeLabel";

		public const string TimeLeftColumn = "TimeLeftColumn";

		public const string To = "To";

		public const string TogglesTheAlarmToBeEnabledDisabled = "TogglesTheAlarmToBeEnabledDisabled";

		public const string TrackerTab = "TrackerTab";

		public const string TriggeredState = "TriggeredState";

		public const string TypeLabel = "TypeLabel";

		public const string UmbralWind = "UmbralWind";

		public const string UnableToFindAnyPublicTrackers = "UnableToFindAnyPublicTrackers";

		public const string UnableToGetVersion = "UnableToGetVersion";

		public const string Unknown = "Unknown";

		public const string UpdateButton = "UpdateButton";

		public const string UptimeLabel = "UptimeLabel";

		public const string UseChatSoundEffectsSetting = "UseChatSoundEffectsSetting";

		public const string UseChatSoundEffectsTooltip = "UseChatSoundEffectsTooltip";

		public const string VersionLabel = "VersionLabel";

		public const string Water = "Water";

		public const string WeatherForecast = "WeatherForecast";

		public const string WeatherRequired = "WeatherRequired";

		public const string WeatherTimersForImportantNms = "WeatherTimersForImportantNms";

		public const string WeatherLabel = "WeatherLabel";

		public const string WillBeUpForTheNext = "WillBeUpForTheNext";

		public const string WillBeUpIn = "WillBeUpIn";

		public const string WillShowTheLevelOfAGivenNmInTheTrackerTable = "WillShowTheLevelOfAGivenNmInTheTrackerTable";

		public const string Wind = "Wind";

		public const string WowYouCanNowEditAlarms = "WowYouCanNowEditAlarms";

		public const string CustomMessagesHowToUse = "CustomMessagesHowToUse";

		public const string DisplayServerIdInServerInfoBar = "DisplayServerIdInServerInfoBar";

		public const string ElementalCrowdsourceContactMessage = "ElementalCrowdsourceContactMessage";

		public const string ElementalCrowdsourceOptOutMessage = "ElementalCrowdsourceOptOutMessage";

		public const string AutoPopFateWithinRangeTooltip = "AutoPopFateWithinRangeTooltip";

		public const string InstanceServerIdDisclaimer = "InstanceServerIdDisclaimer";

		public const string AboutIntro = "AboutIntro";

		public const string RelicRequirementsTooltip = "RelicRequirementsTooltip";

		public const string YouMustBeInOneOfTheEurekaZoneToUseThis = "YouMustBeInOneOfTheEurekaZoneToUseThis";

		public const string YouMustBeInTheSameZoneToPlaceAMarker = "YouMustBeInTheSameZoneToPlaceAMarker";

		public const string YourAlarms = "YourAlarms";

		public const string ZoneLabel = "ZoneLabel";
	}

	private static readonly Dictionary<string, string> UiTextKeys = new Dictionary<string, string>
	{
		["TimeAgo"] = "- TIME AGO -",
		["BossnameReplacedWithFateBossName"] = "%%bossName%% - Replaced with fate boss name",
		["BossshortnameReplacedWithFateBossShortName"] = "%%bossShortName%% - Replaced with fate boss short name",
		["FatenameReplacedWithFateName"] = "%%fateName%% - Replaced with fate name",
		["FlagReplacedWithFlag"] = "%%flag%% - Replaced with <flag>",
		["Day"] = "(Day)",
		["Night"] = "(Night)",
		["AvailableFormattings"] = "** AVAILABLE FORMATTINGS **",
		["DisclaimerReadThis"] = "** DISCLAIMER, READ THIS **",
		["ArisuDisplayNextWeatherForCrabCassieSkoll"] = "/arisu -> Display next weather for Crab, Cassie & Skoll",
		["EalarmsOpensClosesTheEurekaAlarmsWindow"] = "/ealarms -> Opens / Closes the Eureka Alarms window",
		["ErelicOpensClosesTheEurekaRelicHelperWindow"] = "/erelic -> Opens / Closes the Eureka Relic helper window",
		["EtrackersAttemptsToGetATrackerForTheCurrentInstanceInTheSameDatacenter"] = "/etrackers -> Attempts to get a tracker for the current instance in the same datacenter.",
		["EurekahelperEhEhelperOpensClosesTheConfigurationWindow"] = "/eurekahelper | /eh | /ehelper -> Opens / Closes the configuration window",
		["ClickTo0"] = "[Click to {0}]",
		["Key0123Ago"] = "{0} {1} {2} {3} ago",
		["Key0In1"] = "{0} in: {1}",
		["Key0ServerId1"] = "{0} Server ID: {1}",
		["Key0WeatherIsUpNowItEndsIn"] = "{0} weather is up now! It ends in ",
		["HasBeenPoppedAndIsOnARespawnTimer"] = "=> Has been popped and is on a respawn timer",
		["OneOfTheRequirementsIsNotMetToSpawnPrepTheNm"] = "=> One of the requirements is not met to spawn/prep the NM",
		["ReadyToBeSpawned"] = "=> Ready to be spawned",
		["ASoundQueWillBePlayedWheneverAnNmPops"] = "A sound que will be played whenever an NM pops.",
		["AboutTab"] = "About",
		["AboutSection"] = "About:",
		["AddButton"] = "Add",
		["AddAlarm"] = "Add Alarm",
		["AddAnAlarm"] = "Add an alarm",
		["AddKnownElementalMapMarkers"] = "Add Known Elemental Map Markers",
		["AddKnownElementalMapMarkersTooltip"] = "Adds a marker to known Elemental positions on the current map and minimap.\nHelp contribute to the known locations by providing the developer the necessary information",
		["AlarmConfigurationsEdit"] = "Alarm Configurations / Edit",
		["AlarmInformation"] = "Alarm Information",
		["AlarmName"] = "Alarm Name",
		["AlwaysClearElementalsSetting"] = "Always Clear Elementals",
		["AlwaysClearTheElementalListWheneverYouJoinAEurekaZone"] = "Always clear the Elemental list whenever you join a Eureka zone",
		["AnemosTab"] = "Anemos",
		["AssistToCrowdsourceForElementalLocations"] = "Assist to crowdsource for Elemental locations",
		["AttemptsToAutoPopFateWhenConnectedToATrackerIfYouHaveThePassword"] = "Attempts to auto pop fate when connected to a tracker (if you have the password)",
		["AttemptsToGetATrackerForTheCurrentInstanceInTheSameDatacenter"] = "Attempts to get a tracker for the current instance in the same datacenter.",
		["AutoCreateTrackerSetting"] = "Auto Create Tracker",
		["AutoCreatesTrackerWhenJoiningAnInstanceAndPrintsTheTrackerLinkToChat"] = "Auto creates tracker when joining an instance and prints the tracker link to chat",
		["AutoMarkElementalsSetting"] = "Auto Mark Elementals",
		["AutoMarkElementalsTooltip"] = "Auto mark Elementals (only new Elementals) on map as you find them.\nDue to some limitations, the map will always open when you find an Elemental with this configuration enabled.",
		["AutoPopFateSetting"] = "Auto pop fate",
		["AutoPopFateWithinRangeSetting"] = "Auto Pop fate within range",
		["Bedo9041ForEurekaplugin"] = "Bedo9041 for EurekaPlugin",
		["Blizzards"] = "Blizzards",
		["BunnySoundEffectSetting"] = "Bunny Sound Effect",
		["ButtonColumn"] = "Button",
		["ChatChannelsSetting"] = "Chat Channels",
		["ChatSoundEffectToBePlayedWhenAnNmPops"] = "Chat Sound Effect to be played when an NM pops.",
		["ClearAllElementals"] = "Clear All Elementals",
		["ClearAllMapMarkers"] = "Clear All Map Markers",
		["CodeColumn"] = "Code",
		["CodeLabel"] = "Code:",
		["CommandsSection"] = "Commands",
		["ConfigurationTab"] = "Configuration",
		["Confirm"] = "Confirm?",
		["ContactSection"] = "Contact:",
		["CopyAction"] = "copy",
		["CopyTrackerLinkToClipboard"] = "Copy tracker link to clipboard",
		["CopyTrackerPasswordToClipboard"] = "Copy tracker password to clipboard",
		["CreateANewTracker"] = "Create a new tracker",
		["CreateAnemosTracker"] = "Create Anemos Tracker",
		["CreateHydatosTracker"] = "Create Hydatos Tracker",
		["CreatePagosTracker"] = "Create Pagos Tracker",
		["CreatePyrosTracker"] = "Create Pyros Tracker",
		["CreditsSection"] = "Credits:",
		["CrowdsourceLocationsSetting"] = "Crowdsource Locations",
		["CustomMessages"] = "Custom Messages",
		["DayIn0"] = "Day in {0}",
		["DeleteColumn"] = "Delete",
		["DeleteAll"] = "Delete All",
		["DeleteTheCurrentAlarm"] = "Delete the current alarm",
		["DisplayAToastWheneverTheAlarmIsTriggered"] = "Display a toast whenever the alarm is triggered",
		["DisplayElementalSetting"] = "Display Elemental",
		["DisplayElementalToastSetting"] = "Display Elemental Toast",
		["DisplayFateProgressSetting"] = "Display fate progress",
		["DisplayNextWeatherForCrabCassieSkoll"] = "Display next weather for Crab, Cassie & Skoll.",
		["DisplayNmPopSetting"] = "Display NM Pop",
		["DisplayServerIdInChatSetting"] = "Display Server Id in chat",
		["DisplayToastSetting"] = "Display Toast",
		["DisplaysAToastWheneverAnElementalAppearsNearThePlayer"] = "Displays a toast whenever an Elemental appears near the player",
		["DisplaysAToastWheneverAnNmPops"] = "Displays a toast whenever an NM pops",
		["DisplaysInChatWheneverAnElementalAppearsNearThePlayer"] = "Displays in chat whenever an Elemental appears near the player",
		["DisplaysTheNmThatPoppedInChat"] = "Displays the NM that popped in chat",
		["DoneColumn"] = "Done",
		["ET"] = "E.T:",
		["Earth"] = "Earth",
		["EditAlarm"] = "Edit Alarm",
		["EditTheCurrentAlarm"] = "Edit the current alarm",
		["Electr0sheepForEurekatrackerautopopper"] = "electr0sheep for EurekaTrackerAutoPopper",
		["Element"] = "Element:",
		["ElementalColumn"] = "Elemental",
		["ElementalFoundThatIsNotInThePluginDatabase"] = "Elemental found that is not in the plugin database.",
		["ElementalPayloadCopyTooltip"] = "Copies \"{name} <flag>\" to the clipboard after setting a flag marker.",
		["ElementalPayloadOptionsTooltip"] = "Sets what the clickable payload does.\nThis also affects the Shout/Copy column in the table.\nFor example: Setting it to 'ShoutToChat' will send the Elemental to current chat when you click the button.",
		["ElementalPayloadShoutTooltip"] = "Sends \"/sh {name} <flag>\" to chat after setting a flag marker.",
		["ElementalsTab"] = "Elementals",
		["EnableBunnyFatesSetting"] = "Enable bunny fates",
		["EnableDisplayForBunnyFates"] = "Enable display for bunny fates",
		["EnableNmPopSoundSetting"] = "Enable NM pop sound",
		["EndLabel"] = "End:",
		["EndsIn0"] = "Ends in {0}",
		["Enter6DigitCode"] = "Enter 6 digit code",
		["EnterTrackerPassword"] = "Enter tracker password",
		["EurekaHelper"] = "Eureka Helper",
		["EurekaHelperAlarms"] = "Eureka Helper - Alarms",
		["EurekaHelperRelic"] = "Eureka Helper - Relic",
		["ExportsTheCurrentTrackerToANewOne"] = "Exports the current tracker to a new one",
		["FailedToGetValueForSomeReasonPleaseContactAuthor"] = "Failed to get value for some reason, please contact author.",
		["FairSkies"] = "Fair Skies",
		["FateLevel0"] = "FATE Level: {0}",
		["FateName0"] = "FATE Name: {0}",
		["FfxivDevCommunity"] = "FFXIV Dev community",
		["Fire"] = "Fire",
		["Fog"] = "Fog",
		["Found"] = "Found",
		["Gales"] = "Gales",
		["GitHubLabel"] = "GitHub:",
		["Gloom"] = "Gloom",
		["GreenLegend"] = "Green",
		["HeatWaves"] = "Heat Waves",
		["HydatosTab"] = "Hydatos",
		["Ice"] = "Ice",
		["Id0TTviewers1"] = "ID: {0}\\t\\tViewers: {1}",
		["In"] = "in ",
		["In0"] = "in: {0}",
		["InformationSection"] = "Information:",
		["InstanceTab"] = "Instance",
		["InvalidTracker"] = "Invalid Tracker",
		["InventoriesLabel"] = "Inventories:",
		["IsAt"] = "is at",
		["IssuesFeedbackLabel"] = "Issues / Feedbacks:",
		["ItemColumn"] = "Item",
		["JobCategoryColumn"] = "Job Category",
		["JoinsATrackerWithTheSpecifiedIdAndPassword"] = "Joins a tracker with the specified ID and password",
		["KangaszForEurekahelperContributions"] = "KangasZ for EurekaHelper contributions",
		["LastSeen"] = "Last Seen",
		["LeaveTheCurrentTracker"] = "Leave the current tracker",
		["Lightning"] = "Lightning",
		["LinkedItem"] = "Linked Item: ",
		["LocationColumn"] = "Location",
		["LevelColumn"] = "Lv",
		["MarkColumn"] = "Mark",
		["MinutesBefore"] = "Minutes Before:",
		["AlarmNameHint"] = "Name of alarm",
		["NameLabel"] = "Name:",
		["Next0Weather1In"] = "Next {0} weather ({1}) in ",
		["NightIn0"] = "Night in {0}",
		["NightRequired"] = "Night Required",
		["NmColumn"] = "NM",
		["NmSoundEffectSetting"] = "NM Sound Effect",
		["Nm0"] = "NM: {0}",
		["WeatherNone"] = "None",
		["NotConnectedToATracker"] = "Not connected to a tracker",
		["Note"] = "Note:",
		["OpensClosesTheConfigurationWindow"] = "Opens / Closes the configuration window",
		["OpensClosesTheEurekaAlarmsWindow"] = "Opens / Closes the Eureka Alarms window",
		["OpensClosesTheEurekaRelicHelperWindow"] = "Opens / Closes the Eureka Relic helper window",
		["OpensTheTrackerInABrowser"] = "Opens the tracker in a browser",
		["OrangeLegend"] = "Orange",
		["PagosTab"] = "Pagos",
		["PasswordColumn"] = "Password",
		["PasswordLabel"] = "Password:",
		["Password0"] = "Password: {0}",
		["PayloadOptionsSetting"] = "Payload Options",
		["PayloadOptionsTooltip"] = "Sets what the clickable payload does.\nFor example: Setting it to 'ShoutToChat' will shout the pop when you click the button in chat.",
		["PopButton"] = "POP",
		["PoppedAt"] = "Popped At",
		["PoppedOn0LocalTime"] = "Popped on {0} local time",
		["PrintsAMessageWheneverTheAlarmIsTriggered"] = "Prints a message whenever the alarm is triggered",
		["PrintsTheNmProgressInChat"] = "Prints the NM progress in chat",
		["PublicTrackers"] = "public trackers:",
		["PyrosTab"] = "Pyros",
		["RandomizeMapCoords"] = "Randomize Map Coords",
		["RandomizesMapCoordsToRangeOf05RecommendedToEnable"] = "Randomizes map coords to range of +- 0.5 (recommended to enable)",
		["ReadyState"] = "Ready",
		["RedLegend"] = "Red",
		["RequirementsLabel"] = "Requirements:",
		["ResetButton"] = "RESET",
		["ResetAll"] = "Reset All",
		["RespawnIn"] = "Respawn In",
		["RespawnTimeMayBeWrongDueToOtherConditions"] = "Respawn time may be wrong due to other conditions",
		["RetainersLabel"] = "Retainers:",
		["SC"] = "S/C",
		["SaddlebagsLabel"] = "Saddlebags:",
		["ServerId0"] = "Server ID: {0}",
		["SetButton"] = "Set",
		["SetTheChannelWhichThePluginMessagesWillDisplayDefaultEcho"] = "Set the channel which the plugin messages will display. Default: Echo",
		["SetTrackerToPrivate"] = "Set tracker to private",
		["SetTrackerToPublic"] = "Set tracker to public",
		["SettingsLabel"] = "Settings:",
		["ShoutAction"] = "shout",
		["ShowLevelOnTrackerSetting"] = "Show Level On Tracker",
		["Showers"] = "Showers",
		["Snow"] = "Snow",
		["SomethingWentWrongPleaseContactTheAuthorN0"] = "Something went wrong. Please contact the author.\\n{0}",
		["SoundEffectToBePlayedWhenAnNmPops"] = "Sound Effect to be played when an NM pops.",
		["SoundEffectToBePlayedWhenBunnySpawns"] = "Sound Effect to be played when bunny spawns.",
		["SoundEffectLabel"] = "Sound Effect:",
		["SpawnedBy"] = "Spawned By",
		["StartLabel"] = "Start:",
		["SuccessfullyCreatedATracker0"] = "Successfully created a tracker: {0}",
		["SuccessfullyExportedThePreviousTracker0"] = "Successfully exported the previous tracker: {0}",
		["ThisDatacenterIsNotSupportedCurrentlyPleaseSubmitAnIssueIfYouThinkThisIsIncorrect"] = "This datacenter is not supported currently. Please submit an issue if you think this is incorrect.",
		["Thunder"] = "Thunder",
		["Thunderstorms"] = "Thunderstorms",
		["TimeLabel"] = "Time:",
		["TimeLeftColumn"] = "Timeleft",
		["To"] = "to ",
		["TogglesTheAlarmToBeEnabledDisabled"] = "Toggles the alarm to be enabled/disabled",
		["TrackerTab"] = "Tracker",
		["TriggeredState"] = "Triggered",
		["TypeLabel"] = "Type:",
		["UmbralWind"] = "Umbral Wind",
		["UnableToFindAnyPublicTrackers"] = "Unable to find any public trackers.",
		["UnableToGetVersion"] = "Unable to get version",
		["Unknown"] = "Unknown",
		["UpdateButton"] = "Update",
		["UptimeLabel"] = "Uptime",
		["UseChatSoundEffectsSetting"] = "Use Chat Sound Effects",
		["UseChatSoundEffectsTooltip"] = "This option can be enabled to use the chat sound effects instead of the other sound effects.\nThis option is active for ALL sound effects and will not overwrite your previous selections, however you will need to reset the sound effects.",
		["VersionLabel"] = "Version:",
		["Water"] = "Water",
		["WeatherForecast"] = "Weather Forecast:",
		["WeatherRequired"] = "Weather Required:",
		["WeatherTimersForImportantNms"] = "Weather timers for important NMs:",
		["WeatherLabel"] = "Weather:",
		["WillBeUpForTheNext"] = "will be up for the next ",
		["WillBeUpIn"] = "will be up in ",
		["WillShowTheLevelOfAGivenNmInTheTrackerTable"] = "Will show the level of a given NM in the tracker table.",
		["Wind"] = "Wind",
		["WowYouCanNowEditAlarms"] = "Wow, you can now edit alarms.",
		["CustomMessagesHowToUse"] = "** HOW TO USE **\nType the messages you want in each line, to enter the next line press \"Enter\"\n",
		["DisplayServerIdInServerInfoBar"] = "Display Server Id in \"server info\" bar",
		["ElementalCrowdsourceContactMessage"] = "Please send the following information to the developer on GitHub or Discord DM. You can find the contact information in the \"About\" tab.",
		["ElementalCrowdsourceOptOutMessage"] = "You can also opt-out of crowdsourcing for Elemental positions in the \"Elementals\" tab.",
		["AutoPopFateWithinRangeTooltip"] = "Requires \"Auto pop fate\" to be enabled.\n\nNM fates has an estimated respawn time of 2 hours\nThis option will pop fates if it has a cooldown of less than 5 minutes instead of waiting for the normal 2 hour duration",
		["InstanceServerIdDisclaimer"] = "This option will display the current server ID of the instance in chat each time you instance into a Eureka zone. This might help you identify unique instances. However, there are a few things you should note.\n\nFirst of all, this method is definitely not the best way to uniquely identify Eureka zones.\n\nSecondly, according to sources and self-testing, the server ID may get reused for the new instance after the old instance gets locked.\n\nExamples:\nIf you enter a Pagos zone with server ID (60) and you rejoin to another Pagos zone with server ID (61), it would have meant that you've just joined another instance.\nIf a zone in Pyros with server ID (59) gets locked, on very rare occasions, the new Pyros instance might get the same server ID (59) as well.\n\nThirdly, from what I know and have read (but have been unable to test), these server IDs are unique to people in the same world as you. This means that another person in another world will get a different server ID than what you have.\n\nAfter reading all this information, I hope that you will use it only for your own good. And I will not be entertaining any feedback mentioning that the server ID is \"incorrect\".",
		["AboutIntro"] = "Hi there!\nThis is my first FFXIV plugin, alot of the ideas are shamelessly taken from other plugins.\n\nWelcome to Eureka Helper, a tool to help you on your Eureka Adventures. It offers a small variety of QoL changes and a built-in Eureka Tracker.\nFor those interested in money making NMs (e.g Cassie, Skoll), you can type /arisu (command name from ABBA discord) for their next weather time window!",
		["RelicRequirementsTooltip"] = "These are the requirements you need to complete/gather for each relic stage\n\nThe first number shows the amount you have while the second number shows the amount you need\nHowever, for \"Elemental +1\" and \"Elemental +2\" armor relics, the numbers varies from 30-50 and 21-35 respectively\nTo keep it simple, requirements will only show the maximum number of items needed\n\nIf you keep the items in other inventories (ex. Saddlebags, Retainers), you need to open it at least once to update the count.",
		["YouMustBeInOneOfTheEurekaZoneToUseThis"] = "You must be in one of the Eureka zone to use this.",
		["YouMustBeInTheSameZoneToPlaceAMarker"] = "You must be in the same zone to place a marker.",
		["YourAlarms"] = "Your Alarms",
		["ZoneLabel"] = "Zone:"
	};

	private static readonly Dictionary<string, string> UiZhTw = new Dictionary<string, string>
	{
		["Tracker"] = "追蹤器",
		["Treasure Hunt"] = "尋寶",
		["Collects the direction hints from using a Lucky Carrot after the Fortune's Rabbit event, and estimates the treasure's location. The estimated range is drawn in-game via Splatoon."] = "收集使用幸運胡蘿蔔（幸福兔事件後取得）時出現的方位提示，推算寶藏位置。推算範圍會透過 Splatoon 畫在遊戲畫面上，同時自動在地圖上標記推算位置。",
		["Clear"] = "清除",
		["Estimated Location"] = "推算位置：",
		["No treasure hints collected yet."] = "尚未收集到任何尋寶提示。",
		["Direction"] = "方位",
		["Distance Tier"] = "距離等級",
		["Click to place a map flag here."] = "點擊在此設置地圖旗標。",
		["Set Map Flag"] = "設置地圖旗標",
		["Splatoon: Connected"] = "Splatoon：已連線",
		["Splatoon: Not Connected (range circle won't be drawn - map flag still works)"] = "Splatoon：未連線（不會畫出範圍圈，但地圖旗標仍會正常標記）",
		["Auto-move to flag (requires vnavmesh)"] = "自動走向旗標（需要 vnavmesh 外掛）",
		["Found History"] = "尋寶歷史",
		["Clear History"] = "清除歷史",
		["Records the coordinates every time you find the treasure, so you can compare them against the hints later to calibrate the distance-tier estimates."] = "每次挖到寶藏時會記錄座標，之後可以跟提示紀錄比對，校正距離等級的估算是否準確。",
		["Hints"] = "提示數",
		["Eureka Helper - Relic"] = "Eureka Helper - 魂武",
		["Eureka Helper - Alarms"] = "Eureka Helper - 鬧鐘",
		["Elementals"] = "聖靈",
		["Mutant Monsters"] = "變異怪物",
		["No mutant monster data for this zone yet."] = "此地圖尚無變異怪物資料。",
		["Status"] = "狀態",
		["Mutated"] = "突然變異",
		["Adapted"] = "環境適應",
		["Coordinates"] = "座標",
		["[Triggering: {0}]"] = "[觸發中: {0}]",
		["Triggering"] = "觸發中",
		["Sync Recent Pops"] = "同步近期觸發紀錄",
		["Fetches pop times reported in the last 2 hours from the community tracker and applies any that are newer than what's currently recorded."] = "從社群追蹤網站抓取過去2小時內回報的觸發時間，套用比目前紀錄更新的資料。",
		["Pull time left: {0}"] = "剩餘回報時間: {0}",
		["{0} people"] = "{0} 人",
		["{0} - Pull time left: {1}"] = "{0} - 剩餘回報時間: {1}",
		["Actions"] = "操作",
		["Reposition"] = "重新定位",
		["Delete"] = "刪除",
		["Overwrites this record's location with where you're currently standing."] = "用目前角色所在位置覆蓋這筆紀錄的座標。",
		["Manual Trigger"] = "手動觸發",
		["Manually sends the vnavmesh move-to-flag command right now, without waiting for a new hint."] = "立即手動發送 vnavmesh 走向旗標指令，不用等待新的尋寶提示。",
		["Click to set a flag marker"] = "點擊設置地圖旗標",
		["Can Mutate"] = "可變異",
		["Can Adapt"] = "可適應",
		["Roams multiple locations"] = "地圖內各關隘處",
		["Configuration"] = "設定",
		["Instance"] = "副本",
		["About"] = "關於",
		["Settings:"] = "設定：",
		["Create a new tracker"] = "建立新的追蹤器",
		["Anemos"] = "常風之地",
		["Pagos"] = "恆冰之地",
		["Pyros"] = "湧火之地",
		["Hydatos"] = "豐水之地",
		["Create Anemos Tracker"] = "建立常風之地追蹤器",
		["Create Pagos Tracker"] = "建立恆冰之地追蹤器",
		["Create Pyros Tracker"] = "建立湧火之地追蹤器",
		["Create Hydatos Tracker"] = "建立豐水之地追蹤器",
		["Copy tracker link to clipboard"] = "複製追蹤器連結到剪貼簿",
		["Copy tracker password to clipboard"] = "複製追蹤器密碼到剪貼簿",
		["Set tracker to private"] = "將追蹤器設為私人",
		["Set tracker to public"] = "將追蹤器設為公開",
		["Opens the tracker in a browser"] = "在瀏覽器開啟追蹤器",
		["Exports the current tracker to a new one"] = "將目前追蹤器匯出成新的追蹤器",
		["Rebuild tracker connection"] = "重建追蹤器連線",
		["Leave the current tracker"] = "離開目前追蹤器",
		["This datacenter is not supported currently. Please submit an issue if you think this is incorrect."] = "目前不支援此資料中心。如果你認為這有誤，請提交 issue。",
		["E.T:"] = "艾奧傑亞時間：",
		["(Night)"] = "（夜晚）",
		["(Day)"] = "（白天）",
		["Day in {0}"] = "{0} 後進入白天",
		["Night in {0}"] = "{0} 後進入夜晚",
		["Weather:"] = "天氣：",
		["Ends in {0}"] = "{0} 後結束",
		["Weather Forecast:"] = "天氣預報：",
		["in: {0}"] = "{0} 後",
		["{0} in: {1}"] = "{0}倒數：{1}",
		["Green"] = "綠色",
		["Red"] = "紅色",
		["Orange"] = "橘色",
		["=> Ready to be spawned"] = "=> 可觸發",
		["=> Has been popped and is on a respawn timer"] = "=> 已觸發，正在重生倒數",
		["=> One of the requirements is not met to spawn/prep the NM"] = "=> 尚未滿足觸發或準備 NM 的其中一項條件",
		["ID: {0}\t\tViewers: {1}"] = "ID: {0}\t\t觀看者：{1}",
		["ID: {0}\t\tServer ID: {1}\t\tViewers: {2}"] = "ID: {0}\t\t伺服器 ID：{1}\t\t觀看者：{2}",
		["Code"] = "代碼",
		["Password"] = "密碼",
		["Button"] = "按鈕",
		["Code:"] = "代碼：",
		["Password:"] = "密碼：",
		["Password: {0}"] = "密碼：{0}",
		["Enter 6 digit code"] = "輸入 6 位數代碼",
		["Enter tracker password"] = "輸入追蹤器密碼",
		["Don't input if you just want to join a tracker.\nIf you have the password, enter the correct password or you'll need to press \"Set\" again."] = "如果只是要加入追蹤器，請不要輸入。\n如果你有密碼，請輸入正確密碼，否則需要再次按下「設定」。",
		["Set"] = "設定",
		["Joins a tracker with the specified ID and password"] = "使用指定 ID 與密碼加入追蹤器",
		["Successfully created a tracker: {0}"] = "已成功建立追蹤器：{0}",
		["Successfully exported the previous tracker: {0}"] = "已成功匯出先前的追蹤器：{0}",
		["Lv"] = "等級",
		["NM"] = "NM",
		["Spawned By"] = "觸發目標",
		["Popped At"] = "觸發時間",
		["Respawn In"] = "重生倒數",
		["Reset All"] = "全部重置",
		["Confirm?"] = "確認？",
		["Invalid Tracker"] = "無效的追蹤器",
		["Not connected to a tracker"] = "尚未連線到追蹤器",
		["FATE Name: {0}"] = "FATE 名稱：{0}",
		["FATE Level: {0}"] = "FATE 等級：{0}",
		["Element:"] = "屬性：",
		["Weather Required:"] = "需要天氣：",
		["Night Required"] = "需要夜晚",
		["Popped on {0} local time"] = "在本地時間 {0} 觸發",
		["- TIME AGO -"] = "- 經過時間 -",
		["NM: {0}"] = "NM：{0}",
		["{0} {1} {2} {3} ago"] = "{0} {1} {2} {3}前",
		["hour"] = "小時",
		["hours"] = "小時",
		["minute"] = "分鐘",
		["minutes"] = "分鐘",
		["Ready"] = "可觸發",
		["Respawn"] = "重生",
		["Night"] = "夜晚",
		["Note:"] = "注意：",
		["Respawn time may be wrong due to other conditions"] = "因其他條件影響，重生時間可能不準確",
		["RESET"] = "重置",
		["POP"] = "觸發",
		["Display Elemental"] = "顯示聖靈",
		["Displays in chat whenever an Elemental appears near the player"] = "聖靈出現在玩家附近時顯示在聊天中",
		["Display Elemental Toast"] = "顯示聖靈通知",
		["Displays a toast whenever an Elemental appears near the player"] = "聖靈出現在玩家附近時顯示通知",
		["Crowdsource Locations"] = "回報位置資料",
		["Assist to crowdsource for Elemental locations"] = "協助蒐集聖靈位置資料",
		["Payload Options"] = "互動選項",
		["Copies \"{name} <flag>\" to the clipboard after setting a flag marker."] = "設置旗標後，將「{name} <flag>」複製到剪貼簿。",
		["Sets what the clickable payload does.\nThis also affects the Shout/Copy column in the table.\nFor example: Setting it to 'ShoutToChat' will send the Elemental to current chat when you click the button."] = "設定可點擊連結的動作。\n這也會影響表格中的喊話/複製欄位。\n例如：設定為「喊話到聊天」時，點擊按鈕會把聖靈資訊送到目前聊天頻道。",
		["Sends \"/sh {name} <flag>\" to chat after setting a flag marker."] = "設置旗標後，將「/sh {name} <flag>」送到聊天。",
		["Auto Mark Elementals"] = "自動標記聖靈",
		["Auto mark Elementals (only new Elementals) on map as you find them.\nDue to some limitations, the map will always open when you find an Elemental with this configuration enabled."] = "發現聖靈時自動在地圖標記（僅限新聖靈）。\n受限於遊戲機制，啟用後發現聖靈時會一律開啟地圖。",
		["Always Clear Elementals"] = "永遠清除聖靈清單",
		["Always clear the Elemental list whenever you join a Eureka zone"] = "每次進入優雷卡區域時清除聖靈清單",
		["Add Known Elemental Map Markers"] = "加入已知聖靈地圖標記",
		["You must be in one of the Eureka zone to use this."] = "你必須位於任一優雷卡區域才能使用此功能。",
		["Adds a marker to known Elemental positions on the current map and minimap.\nHelp contribute to the known locations by providing the developer the necessary information"] = "在目前地圖與小地圖上加入已知聖靈位置標記。\n你也可以向開發者提供必要資訊，協助完善已知位置。",
		["Clear All Elementals"] = "清除所有聖靈",
		["Clear All Map Markers"] = "清除所有地圖標記",
		["Elemental"] = "聖靈",
		["Location"] = "位置",
		["Last Seen"] = "最後發現",
		["S/C"] = "喊/複",
		["Mark"] = "標記",
		["Delete"] = "刪除",
		["Display NM Pop"] = "顯示 NM 觸發",
		["Displays the NM that popped in chat"] = "在聊天中顯示已觸發的 NM",
		["Enable NM pop sound"] = "啟用 NM 觸發音效",
		["A sound que will be played whenever an NM pops."] = "NM 觸發時播放音效。",
		["Display fate progress"] = "顯示 FATE 進度",
		["Prints the NM progress in chat"] = "在聊天中顯示 NM 進度",
		["Enable bunny fates"] = "啟用兔兔 FATE",
		["Enable display for bunny fates"] = "顯示兔兔 FATE",
		["Display Toast"] = "顯示通知",
		["Displays a toast whenever an NM pops"] = "NM 觸發時顯示通知",
		["Auto pop fate"] = "自動標記 FATE 觸發",
		["Attempts to auto pop fate when connected to a tracker (if you have the password)"] = "連線到追蹤器時嘗試自動標記 FATE 觸發（需要密碼）",
		["NM Sound Effect"] = "NM 音效",
		["Chat Sound Effect to be played when an NM pops."] = "NM 觸發時播放的聊天音效。",
		["Sound Effect to be played when an NM pops."] = "NM 觸發時播放的音效。",
		["Bunny Sound Effect"] = "兔兔音效",
		["Sound Effect to be played when bunny spawns."] = "兔兔出現時播放的音效。",
		["Sets what the clickable payload does.\nFor example: Setting it to 'ShoutToChat' will shout the pop when you click the button in chat."] = "設定可點擊連結的動作。\n例如：設定為「喊話到聊天」時，點擊聊天中的按鈕會喊出觸發資訊。",
		["Chat Channels"] = "聊天頻道",
		["Set the channel which the plugin messages will display. Default: Echo"] = "設定外掛訊息顯示的頻道。預設：Echo",
		["Randomize Map Coords"] = "隨機化地圖座標",
		["Randomizes map coords to range of +- 0.5 (recommended to enable)"] = "將地圖座標隨機偏移 +- 0.5（建議啟用）",
		["Auto Create Tracker"] = "自動建立追蹤器",
		["Auto creates tracker when joining an instance and prints the tracker link to chat"] = "進入副本時自動建立追蹤器，並將連結顯示在聊天中",
		["Auto Pop fate within range"] = "冷卻接近時自動標記 FATE",
		["Requires \"Auto pop fate\" to be enabled.\n\nNM fates has an estimated respawn time of 2 hours\nThis option will pop fates if it has a cooldown of less than 5 minutes instead of waiting for the normal 2 hour duration"] = "需要啟用「自動標記 FATE 觸發」。\n\nNM FATE 預估重生時間為 2 小時。\n啟用後，若冷卻時間小於 5 分鐘，將直接標記 FATE 觸發，而不是等待完整 2 小時。",
		["Show Level On Tracker"] = "追蹤器顯示等級",
		["Will show the level of a given NM in the tracker table."] = "在追蹤器表格中顯示指定 NM 的等級。",
		["Use Chat Sound Effects"] = "使用聊天音效",
		["This option can be enabled to use the chat sound effects instead of the other sound effects.\nThis option is active for ALL sound effects and will not overwrite your previous selections, however you will need to reset the sound effects."] = "啟用後會使用聊天音效取代其他音效。\n此選項會套用到所有音效，且不會覆蓋你先前的選擇，但你需要重新設定音效。",
		["Custom Messages"] = "自訂訊息",
		["** HOW TO USE **\nType the messages you want in each line, to enter the next line press \"Enter\"\n"] = "** 使用方式 **\n每行輸入一則你想使用的訊息，按下 Enter 換行。\n",
		["** AVAILABLE FORMATTINGS **"] = "** 可用格式 **",
		["%%bossName%% - Replaced with fate boss name"] = "%%bossName%% - 替換為 FATE Boss 名稱",
		["%%bossShortName%% - Replaced with fate boss short name"] = "%%bossShortName%% - 替換為 FATE Boss 簡稱",
		["%%fateName%% - Replaced with fate name"] = "%%fateName%% - 替換為 FATE 名稱",
		["%%flag%% - Replaced with <flag>"] = "%%flag%% - 替換為 <flag>",
		["Display Server Id in chat"] = "在聊天中顯示伺服器 ID",
		["Display Server Id in \"server info\" bar"] = "在「伺服器資訊」列顯示伺服器 ID",
		["** DISCLAIMER, READ THIS **"] = "** 免責聲明，請閱讀 **",
		["This option will display the current server ID of the instance in chat each time you instance into a Eureka zone. This might help you identify unique instances. However, there are a few things you should note.\n\nFirst of all, this method is definitely not the best way to uniquely identify Eureka zones.\n\nSecondly, according to sources and self-testing, the server ID may get reused for the new instance after the old instance gets locked.\n\nExamples:\nIf you enter a Pagos zone with server ID (60) and you rejoin to another Pagos zone with server ID (61), it would have meant that you've just joined another instance.\nIf a zone in Pyros with server ID (59) gets locked, on very rare occasions, the new Pyros instance might get the same server ID (59) as well.\n\nThirdly, from what I know and have read (but have been unable to test), these server IDs are unique to people in the same world as you. This means that another person in another world will get a different server ID than what you have.\n\nAfter reading all this information, I hope that you will use it only for your own good. And I will not be entertaining any feedback mentioning that the server ID is \"incorrect\"."] = "此選項會在你每次進入優雷卡區域時，於聊天中顯示目前副本的伺服器 ID。這可能有助於辨識不同副本，但請注意以下事項。\n\n首先，這絕對不是唯一識別優雷卡區域的最佳方法。\n\n其次，根據資料來源與自行測試，舊副本鎖定後，新副本可能會重複使用相同的伺服器 ID。\n\n範例：\n如果你進入恆冰時伺服器 ID 為 (60)，重新進入另一個恆冰時伺服器 ID 為 (61)，代表你剛加入了另一個副本。\n如果湧火某個伺服器 ID 為 (59) 的副本鎖定，在非常少見的情況下，新的湧火副本也可能取得相同的伺服器 ID (59)。\n\n第三，根據我目前知道與讀到的資訊（但尚無法測試），這些伺服器 ID 只對同世界玩家唯一。也就是說，其他世界的玩家會看到不同於你的伺服器 ID。\n\n閱讀完這些資訊後，請只將它用於你自己的判斷。我不會處理任何提到伺服器 ID「不正確」的回饋。",
		["About:"] = "關於：",
		["Hi there!\nThis is my first FFXIV plugin, alot of the ideas are shamelessly taken from other plugins.\n\nWelcome to Eureka Helper, a tool to help you on your Eureka Adventures. It offers a small variety of QoL changes and a built-in Eureka Tracker.\nFor those interested in money making NMs (e.g Cassie, Skoll), you can type /arisu (command name from ABBA discord) for their next weather time window!"] = "你好！\n這是我的第一個 FFXIV 外掛，許多靈感直接取自其他外掛。\n\n歡迎使用 Eureka Helper，這是一個協助你進行優雷卡冒險的工具。它提供一些便利功能，並內建優雷卡追蹤器。\n如果你對賺錢 NM（例如 Cassie、Skoll）有興趣，可以輸入 /arisu（ABBA Discord 的指令名稱）查看下一個天氣時間窗口！",
		["Information:"] = "資訊：",
		["Version:"] = "版本：",
		["Contact:"] = "聯絡：",
		["Issues / Feedbacks:"] = "Issue / 回饋：",
		["Commands"] = "指令",
		["/eurekahelper | /eh | /ehelper -> Opens / Closes the configuration window"] = "/eurekahelper | /eh | /ehelper -> 開啟/關閉設定視窗",
		["/etrackers -> Attempts to get a tracker for the current instance in the same datacenter."] = "/etrackers -> 嘗試取得同資料中心目前副本的追蹤器。",
		["/erelic -> Opens / Closes the Eureka Relic helper window"] = "/erelic -> 開啟/關閉優雷卡魂武助手視窗",
		["/ealarms -> Opens / Closes the Eureka Alarms window"] = "/ealarms -> 開啟/關閉優雷卡鬧鐘視窗",
		["/arisu -> Display next weather for Crab, Cassie & Skoll"] = "/arisu -> 顯示 Crab、Cassie 與 Skoll 的下一次天氣",
		["Credits:"] = "致謝：",
		["Requirements:"] = "需求：",
		["These are the requirements you need to complete/gather for each relic stage\n\nThe first number shows the amount you have while the second number shows the amount you need\nHowever, for \"Elemental +1\" and \"Elemental +2\" armor relics, the numbers varies from 30-50 and 21-35 respectively\nTo keep it simple, requirements will only show the maximum number of items needed\n\nIf you keep the items in other inventories (ex. Saddlebags, Retainers), you need to open it at least once to update the count."] = "這些是每個魂武階段需要完成或收集的項目。\n\n第一個數字代表你目前擁有的數量，第二個數字代表需要的數量。\n不過「Elemental +1」與「Elemental +2」防具魂武的需求數量分別會在 30-50 與 21-35 之間變動。\n為了簡化顯示，需求只會顯示所需物品的最大數量。\n\n如果物品放在其他庫存（例如陸行鳥鞍囊、雇員），你需要至少開啟一次該庫存才能更新數量。",
		["Inventories:"] = "背包：",
		["Saddlebags:"] = "陸行鳥背包：",
		["Retainers:"] = "雇員：",
		["Failed to get value for some reason, please contact author."] = "因不明原因無法取得數值，請聯絡作者。",
		["Item"] = "物品",
		["Job Category"] = "職業分類",
		["Done"] = "完成",
		["Add an alarm"] = "新增鬧鐘",
		["Add Alarm"] = "新增鬧鐘",
		["Edit Alarm"] = "編輯鬧鐘",
		["Name:"] = "名稱：",
		["Name of alarm"] = "鬧鐘名稱",
		["Type:"] = "類型：",
		["Zone:"] = "區域：",
		["Time:"] = "時間：",
		["Sound Effect:"] = "音效：",
		["Minutes Before:"] = "提前分鐘：",
		["Add"] = "新增",
		["Update"] = "更新",
		["Delete All"] = "全部刪除",
		["Wow, you can now edit alarms."] = "現在可以編輯鬧鐘。",
		["Your Alarms"] = "你的鬧鐘",
		["Alarm Name"] = "鬧鐘名稱",
		["Timeleft"] = "剩餘時間",
		["Alarm Configurations / Edit"] = "鬧鐘設定 / 編輯",
		["Alarm Information"] = "鬧鐘資訊",
		["Triggered"] = "已觸發",
		["Uptime"] = "持續時間",
		["Start:"] = "開始：",
		["End:"] = "結束：",
		["in "] = "於 ",
		["will be up in "] = "將於 ",
		["will be up for the next "] = "接下來會持續 ",
		["to "] = "直到 ",
		["Toggles the alarm to be enabled/disabled"] = "切換鬧鐘啟用/停用",
		["Prints a message whenever the alarm is triggered"] = "鬧鐘觸發時顯示訊息",
		["Display a toast whenever the alarm is triggered"] = "鬧鐘觸發時顯示通知",
		["Edit the current alarm"] = "編輯目前鬧鐘",
		["Delete the current alarm"] = "刪除目前鬧鐘",
		["Unnamed"] = "未命名",
		["Weather timers for important NMs:"] = "重要 NM 的天氣計時：",
		["Unable to find any public trackers."] = "找不到任何公開追蹤器。",
		["Found"] = "找到",
		["public trackers:"] = "個公開追蹤器：",
		["Opens / Closes the configuration window"] = "開啟/關閉設定視窗",
		["Display next weather for Crab, Cassie & Skoll."] = "顯示 Crab、Cassie 與 Skoll 的下一次天氣。",
		["Attempts to get a tracker for the current instance in the same datacenter."] = "嘗試取得同資料中心目前副本的追蹤器。",
		["Opens / Closes the Eureka Relic helper window"] = "開啟/關閉優雷卡魂武助手視窗",
		["Opens / Closes the Eureka Alarms window"] = "開啟/關閉優雷卡鬧鐘視窗",
		["Linked Item: "] = "物品連結：",
		["{0} weather is up now! It ends in "] = "{0} 天氣現在已出現！結束倒數 ",
		["Next {0} weather ({1}) in "] = "下一次 {0} 天氣（{1}）倒數 ",
		["You must be in the same zone to place a marker."] = "你必須位於同一區域才能放置標記。",
		["Unable to get version"] = "無法取得版本",
		["is at"] = "目前進度",
		["shout"] = "喊話",
		["copy"] = "複製",
		["[Click to {0}]"] = "[點擊以{0}]",
		["Elemental found that is not in the plugin database."] = "發現外掛資料庫中尚未記錄的聖靈。",
		["Please send the following information to the developer on GitHub or Discord DM. You can find the contact information in the \"About\" tab."] = "請透過 GitHub 或 Discord 私訊將以下資訊傳送給開發者。你可以在「關於」分頁找到聯絡資訊。",
		["You can also opt-out of crowdsourcing for Elemental positions in the \"Elementals\" tab."] = "你也可以在「聖靈」分頁中停用聖靈位置回報。",
		["{0} Server ID: {1}"] = "{0} 伺服器 ID：{1}",
		["Server ID: {0}"] = "伺服器 ID：{0}",
		["Something went wrong. Please contact the author.\n{0}"] = "發生錯誤，請聯絡作者。\n{0}",
		["Weather"] = "天氣",
		["Time"] = "時間",
		["Day"] = "白天",
		["Gales"] = "強風",
		["Showers"] = "驟雨",
		["Fair Skies"] = "晴朗",
		["Snow"] = "雪",
		["Heat Waves"] = "熱浪",
		["Thunder"] = "打雷",
		["Blizzards"] = "暴雪",
		["Fog"] = "霧",
		["Umbral Wind"] = "靈風",
		["Thunderstorms"] = "雷雨",
		["Gloom"] = "妖霧",
		["None"] = "無",
		["Wind"] = "風",
		["Water"] = "水",
		["Earth"] = "土",
		["Lightning"] = "雷",
		["Fire"] = "火",
		["Ice"] = "冰",
		["Unknown"] = "未知",
		["PayloadOptions.ShoutToChat"] = "喊話到聊天",
		["PayloadOptions.CopyToClipboard"] = "複製到剪貼簿",
		["PayloadOptions.Nothing"] = "不執行",
		["AlarmType.Weather"] = "天氣",
		["AlarmType.Time"] = "時間",
		["TimeType.Day"] = "白天",
		["TimeType.Night"] = "夜晚",
		["Logos Actions Unlocked"] = "文理技能",
		["Debug"] = "除錯",
		["Lock a target in-game, tune the shape/radius/color below, and it'll draw live via Splatoon (if connected) so you can compare against the actual aggro range. Hit \"Add to AggroRanges.json\" once it matches."] = "在遊戲中鎖定目標，調整下方的形狀/半徑/顏色，若已連線 Splatoon 會即時畫出來，讓你跟實際的仇恨範圍比對。確認吻合後按下「加入 AggroRanges.json」。",
		["No target locked."] = "尚未鎖定目標。",
		["Name:"] = "名稱：",
		["Data ID:"] = "Data ID：",
		["Kind:"] = "種類：",
		["Distance:"] = "距離：",
		["Aggro type (just a label + starting shape/color - freely editable below)"] = "仇恨類型（只是標籤，會帶入預設形狀/顏色，下方可自由調整）",
		["Radius"] = "半徑",
		["Cone Half-Angle"] = "扇形半角",
		["Color"] = "顏色",
		["Override name (optional)"] = "覆寫名稱（選填）",
		["Leave empty to key the entry by the locked target's exact name."] = "留空的話會直接用鎖定目標的名稱作為索引鍵。",
		["Splatoon isn't connected."] = "尚未連線到 Splatoon。",
		["Add to AggroRanges.json"] = "加入 AggroRanges.json",
		["Existing entries for \"{0}\":"] = "「{0}」目前已有的資料：",
		["Aural"] = "聽覺",
		["Visual"] = "視覺",
		["Magic"] = "魔法",
		["Blood"] = "血親",
		["Other"] = "其他",
		["Circle"] = "圓形",
		["Cone"] = "扇形",
		["Open Relic Window"] = "開啟聖遺物視窗",
		["Close Relic Window"] = "關閉聖遺物視窗",
		["Auto Open Relic Window In Eureka"] = "進入優雷卡時自動開啟聖遺物視窗",
		["Automatically opens the Relic window when you enter a Eureka zone, and closes it when you leave."] = "進入優雷卡區域時自動開啟聖遺物視窗，離開時自動關閉。",
		["Show NM Aggro Ranges (Splatoon)"] = "顯示 NM 仇恨範圍（Splatoon）",
		["Requires the Splatoon plugin. Draws each NM's aural/visual/magic/blood aggro range as a circle/cone.\nEXPERIMENTAL: aggro range data is unverified/incomplete - see AggroRanges.json in the plugin config folder."] = "需要安裝 Splatoon 插件。會將每隻 NM 的聽覺/視覺/魔法/血親仇恨範圍畫成圓形/扇形。\n實驗性功能：仇恨範圍資料尚未驗證/不完整，請參考插件設定資料夾裡的 AggroRanges.json。",
		["Note: Splatoon always draws cones filled (a quirk on its end, not something we can toggle off), so keep the color's alpha low or it'll be an eyesore."] = "注意：Splatoon 畫扇形時一律會實心填滿（這是它本身的特性，沒辦法關掉），建議把顏色的透明度調低一點，不然畫面會很刺眼。",
		["Outline Thickness"] = "外框粗細",
		["Aggro range data file:"] = "仇恨範圍資料檔：",
		["Reload aggro range data"] = "重新載入仇恨範圍資料",
		["Seen Monsters"] = "已發現的怪物",
		["Every mob name encountered in Eureka this session. Names matching a known pattern (e.g. \"Sprite\" -> Magic, undead names -> Blood) get auto-registered with radius 0 - lock them and measure when you get the chance. Everything else defaults to Visual and isn't tracked here individually."] = "本次遊玩期間在優雷卡遇到的所有怪物名稱。符合已知規則的名稱（例如「Sprite」→魔法、不死系名稱→血親）會自動登記半徑 0，有空時鎖定實測填入即可。其餘的一律預設為視覺感知，不會逐一記錄。",
		["{0} unique names seen."] = "共發現 {0} 個不重複名稱。",
		["Diagnostic (last scan): {0} objects -> {1} BattleNpc -> {2} Enemy-kind (not pet/summon) -> {3} alive."] = "診斷（上次掃描）：{0} 個物件 → {1} 個 BattleNpc → {2} 個敵人類型（非寵物/召喚物）→ {3} 個存活中。",
		["Name"] = "名稱",
		["Registered As"] = "已登記為",
		["Visual (default)"] = "視覺（預設）",
		["Re-reads AggroRanges.json after you've edited it, without needing to restart the plugin."] = "修改 AggroRanges.json 後重新讀取，不需要重啟插件。"
	};

	private static readonly Dictionary<string, string> RelicStageZhTw = new Dictionary<string, string>
	{
		["Base"] = "禁地兵裝（武器）",
		["Base +1"] = "禁地兵裝+1（武器）",
		["Base +2"] = "禁地兵裝+2（武器）",
		["Anemos Weapon"] = "常風裝備（武器）",
		["Base Armor"] = "禁地兵裝（防具）",
		["Base +1 Armor"] = "禁地兵裝+1（防具）",
		["Base +2 Armor"] = "禁地兵裝+2（防具）",
		["Anemos Armor"] = "常風裝備（防具）",
		["Pagos Weapon"] = "恆冰武器",
		["Pagos +1"] = "恆冰武器+1",
		["Elemental"] = "元素武器",
		["Elemental +1"] = "元素武器+1",
		["Elemental +2"] = "元素武器+2",
		["Pyros Weapon"] = "湧火武器",
		["Elemental Armor"] = "元素防具",
		["Hydatos Weapon"] = "豐水武器",
		["Hydatos +1"] = "豐水武器+1",
		["Base Eureka"] = "新禁地兵裝",
		["Eureka Weapon"] = "禁地兵裝最終形態",
		["Physeos"] = "禁地兵裝·改裝",
		["Elemental +1 Armor"] = "元素防具+1",
		["Elemental +2 Armor"] = "元素防具+2"
	};

	private static readonly Dictionary<string, string> EurekaNamesZhTw = new Dictionary<string, string>
	{
		["Crab/KA"] = "螃蟹/亞瑟羅王",
		["Cassie"] = "凱西",
		["Sabotender Corrido"] = "寇里多仙人掌怪",
		["Flowering Sabotender"] = "開花仙人掌怪",
		["The Lord of Anemos"] = "常風領主",
		["Sea Bishop"] = "海祭司",
		["Teles"] = "忒勒斯",
		["Anemos Harpeia"] = "常風哈佩亞鳥妖",
		["The Emperor of Anemos"] = "常風皇帝",
		["Darner"] = "晏蜓",
		["Callisto"] = "卡利斯托",
		["Val Bear"] = "瓦爾巨熊",
		["Number"] = "群偶",
		["Pneumaflayer"] = "奪靈魔",
		["Jahannam"] = "哲罕南",
		["Typhoon Sprite"] = "颱風元精",
		["Amemet"] = "阿米特",
		["Abraxas"] = "阿蔔拉克薩斯",
		["Caym"] = "蓋因",
		["Stalker Ziz"] = "追蹤席茲",
		["Bombadeel"] = "龐巴德",
		["Traveling Gourmand"] = "古老貪吃鬼",
		["Serket"] = "塞爾凱特",
		["Khor Claw"] = "河道巨鉗蝦",
		["Judgemental Julika"] = "武斷魔花茱莉卡",
		["Judgmental Julika"] = "武斷魔花茱莉卡",
		["Henbane"] = "天仙子",
		["The White Rider"] = "白騎士",
		["Duskfall Dullahan"] = "黃昏無頭騎士",
		["Polyphemus"] = "波呂斐摩斯",
		["Monoeye"] = "獨眼怪",
		["Simurgh's Strider"] = "闊步西牟鳥",
		["Old World Zu"] = "舊世界祖",
		["King Hazmat"] = "極其危險物質",
		["Anemos Anala"] = "常風阿那羅",
		["Fafnir"] = "法夫納",
		["Fossil Dragon"] = "龍化石",
		["Amarok"] = "阿瑪洛克",
		["Voidscale"] = "虛無鱗龍",
		["Lamashtu"] = "拉瑪什圖",
		["Val Specter"] = "瓦爾妖影",
		["Pazuzu"] = "帕祖祖",
		["Shadow Wraith"] = "暗影幽靈",
		["Khalamari"] = "卡拉墨魚",
		["Xzomit"] = "左米特",
		["Stegodon"] = "劍齒象",
		["Hydatos Primelephas"] = "豐水曙象",
		["Molech"] = "摩洛",
		["Val Nullchu"] = "瓦爾爛泥食腐獸",
		["Piasa"] = "皮艾薩邪鳥",
		["Vivid Gastornis"] = "多彩冠恐鳥",
		["Frostmane"] = "霜鬃獵魔",
		["Northern Tiger"] = "北方猛虎",
		["Daphne"] = "達佛涅",
		["Dark Void Monk"] = "暗黑虛無鬼魚",
		["King Goldemar"] = "戈爾德馬爾王",
		["Hydatos Wraith"] = "豐水幽靈",
		["Leuke"] = "琉刻",
		["Tigerhawk"] = "虎鷹",
		["Barong"] = "巴龍",
		["Laboratory Lion"] = "研究所雄獅",
		["Ceto"] = "刻托",
		["Hydatos Delphyne"] = "豐水達菲妮",
		["Provenance Watcher"] = "起源守望者",
		["Crystal Claw"] = "水晶爪",
		["The Snow Queen"] = "雪之女王",
		["Yukinko"] = "雪童子",
		["Taxim"] = "塔克西姆",
		["Demon of the Incunable"] = "珍卷惡魔",
		["Ash Dragon"] = "灰燼龍",
		["Blood Demon"] = "血魔",
		["Glavoid"] = "異形魔蟲",
		["Val Worm"] = "瓦爾蠕蟲",
		["Anapos"] = "安娜波",
		["Snowmelt Sprite"] = "融雪元精",
		["Hakutaku"] = "白澤",
		["Blubber Eyes"] = "啜泣百目妖",
		["King Igloo"] = "雪屋王",
		["Huwasi"] = "胡瓦西",
		["Asag"] = "阿薩格",
		["Wandering Opken"] = "徘徊歐浦肯",
		["Surabhi"] = "蘇羅毗",
		["Pagos Billygoat"] = "恆冰公山羊",
		["King Arthro"] = "亞瑟羅王",
		["Val Snipper"] = "瓦爾利螯陸蟹",
		["Mindertaur/Eldertaur"] = "牛頭魔看守/牛頭魔長老",
		["Lab Minotaur"] = "研究所米諾陶洛斯",
		["Holy Cow"] = "優雷卡聖牛",
		["Elder Buffalo"] = "古老水牛",
		["Hadhayosh"] = "哈達約什",
		["Lesser Void Dragon"] = "虛無小龍",
		["Horus"] = "荷魯斯",
		["Void Vouivre"] = "虛無薇薇爾飛龍",
		["Arch Angra Mainyu"] = "總領安格拉·曼紐",
		["Gawper"] = "瞪視之眼",
		["Copycat Cassie"] = "複製魔花凱西",
		["Ameretat"] = "阿米雷戴",
		["Louhi"] = "婁希",
		["Val Corpse"] = "瓦爾腐屍",
		["Leucosia"] = "琉科西亞",
		["Pyros Bhoot"] = "湧火浮靈",
		["Flauros"] = "佛勞洛斯",
		["Thunderstorm Sprite"] = "雷暴元精",
		["The Sophist"] = "詭辯者",
		["Pyros Apanda"] = "湧火阿班達",
		["Graffiacane"] = "格拉菲亞卡內",
		["Valking"] = "瓦爾維京人偶",
		["Askalaphos"] = "阿斯卡拉福斯",
		["Overdue Tome"] = "過期魔導書",
		["Grand Duke Batym"] = "巴欽大公爵",
		["Dark Troubadour"] = "暗黑行吟者",
		["Aetolus"] = "艾托洛斯",
		["Islandhander"] = "瓦爾獨爪妖禽",
		["Lesath"] = "來薩特",
		["Bird Eater"] = "食鳥者",
		["Eldthurs"] = "火巨人",
		["Pyros Crab"] = "湧火陸蟹",
		["Iris"] = "伊麗絲",
		["Northern Swallow"] = "北境鹽藍燕",
		["Lamebrix Strikebocks"] = "傭兵雷姆普里克斯",
		["Illuminati Escapee"] = "青藍之手逃亡者",
		["Dux"] = "閃電督軍",
		["Matanga Castaway"] = "遺棄象魔",
		["Lumber Jack"] = "樵夫傑科",
		["Pyros Treant"] = "湧火樹妖",
		["Glaukopis"] = "明眸",
		["Val Skatene"] = "瓦爾斯卡尼特",
		["Ying-Yang"] = "陰·陽",
		["Pyros Hecteyes"] = "湧火百目妖",
		["Skoll"] = "斯庫爾",
		["Pyros Shuck"] = "湧火狗靈",
		["Penthesilea"] = "彭忒西勒亞",
		["Val Bloodglider"] = "瓦爾血飛蛾",
		["Ovni"] = "未確認飛行物體",
		["Tristitia"] = "特里斯提提亞"
	};

	public static string Text(string key)
	{
		if (UiTextKeys.TryGetValue(key, out var value))
		{
			key = UnescapeUiTextKey(value);
		}
		if (!UiZhTw.TryGetValue(key, out var value2))
		{
			return key;
		}
		return value2;
	}

	private static string UnescapeUiTextKey(string text)
	{
		return text.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t");
	}

	public static bool TryEurekaName(string text, out string translated)
	{
		translated = text;
		if (!string.IsNullOrWhiteSpace(text) && EurekaNamesZhTw.TryGetValue(text, out var found))
		{
			translated = found;
			return true;
		}
		return false;
	}

	public static string Format(string format, params object[] args)
	{
		return string.Format(CultureInfo.CurrentCulture, Text(format), args);
	}

	public static string RelicStage(string text)
	{
		if (!RelicStageZhTw.TryGetValue(text, out var value))
		{
			return Text(text);
		}
		return value;
	}

	public static string Enum<T>(T value) where T : struct, Enum
	{
		string key = $"{typeof(T).Name}.{value}";
		if (UiZhTw.TryGetValue(key, out var value2))
		{
			return value2;
		}
		return Text(HumanizeEnumName(value.ToString()));
	}

	public static string[] EnumNames<T>() where T : struct, Enum
	{
		return global::System.Enum.GetValues<T>().Select(Enum).ToArray();
	}

	private static string HumanizeEnumName(string value)
	{
		return EnumNameRegex().Replace(value, " $1");
	}

	private static readonly Regex _enumNameRegexInstance = new Regex("(?<!^)([A-Z])", RegexOptions.Compiled);

	private static Regex EnumNameRegex()
	{
		return _enumNameRegexInstance;
	}
}
