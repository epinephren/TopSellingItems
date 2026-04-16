using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TopSellingItems.Services;
using TopSellingItems.Windows;

namespace TopSellingItems;

public sealed class TopSellingItemsPlugin : IDalamudPlugin
{
    public string Name => "TopSellingItems";
    private const string MainCommand = "/topselling";
    private const string CommandName = "/tpsg";

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IPluginLog PluginLog { get; set; } = null!;
    [PluginService] private static IDataManager DataManager { get; set; } = null!;

    private readonly Configuration configuration;
    private readonly WindowSystem windowSystem = new("TopSellingItems");
    private readonly UniversalisClient universalisClient;
    private readonly ItemScanner itemScanner;
    private readonly WorldService worldService;
    private readonly MarketScanService marketScanService;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    public TopSellingItemsPlugin()
    {
        this.configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(PluginInterface);

        this.universalisClient = new UniversalisClient();
        this.itemScanner = new ItemScanner(DataManager);
        this.worldService = new WorldService(DataManager);

        if (string.IsNullOrWhiteSpace(this.configuration.SelectedDatacenter))
            this.configuration.SelectedDatacenter = this.worldService.GetDatacenters().FirstOrDefault() ?? "Light";

        if (string.IsNullOrWhiteSpace(this.configuration.SelectedWorld))
            this.configuration.SelectedWorld = this.worldService.GetWorldsForDatacenter(this.configuration.SelectedDatacenter).FirstOrDefault()?.WorldName
                ?? this.worldService.GetWorlds().FirstOrDefault()?.WorldName
                ?? "Shiva";
        if (string.IsNullOrWhiteSpace(this.configuration.HomeDatacenter))
            this.configuration.HomeDatacenter = this.configuration.SelectedDatacenter;

        if (string.IsNullOrWhiteSpace(this.configuration.HomeWorld))
            this.configuration.HomeWorld = this.configuration.SelectedWorld;

        this.marketScanService = new MarketScanService(
            PluginLog,
            this.configuration,
            this.universalisClient,
            this.itemScanner,
            this.worldService);
        
        this.mainWindow = new MainWindow(this.configuration, this.marketScanService, this.worldService);
        this.configWindow = new ConfigWindow(this.configuration, this.worldService);

        this.windowSystem.AddWindow(this.mainWindow);
        this.windowSystem.AddWindow(this.configWindow);

        CommandManager.AddHandler(MainCommand, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open the Top Selling Items window.",
        });

        PluginInterface.UiBuilder.Draw += this.DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;

        CommandManager.RemoveHandler(MainCommand);

        this.windowSystem.RemoveAllWindows();
        this.mainWindow.Dispose();
        this.universalisClient.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        this.OpenMainUi();
    }

    private void DrawUi()
    {
        this.configWindow.Flags = this.configuration.IsConfigWindowMovable
            ? ImGuiWindowFlags.None
            : ImGuiWindowFlags.NoMove;

        this.windowSystem.Draw();
    }

    private void OpenMainUi()
    {
        this.mainWindow.IsOpen = true;
    }

    private void OpenConfigUi()
    {
        this.configWindow.IsOpen = true;
    }
}
