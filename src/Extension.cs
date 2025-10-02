using System.Diagnostics;
using System.Reflection;
using Serilog;
using Xabbo;
using Xabbo.Core;
using Xabbo.Core.Game;
using Xabbo.Core.GameData;
using Xabbo.Core.Messages.Outgoing;
using Xabbo.GEarth;

namespace TraderUp;

[Intercept]
internal partial class Extension : GEarthExtension
{
    private static readonly ILogger Logger = Log.ForContext<Extension>();
    private static readonly bool ProcessCreatedSynchronization;

    private static readonly EventWaitHandle Synchronization = new(false, EventResetMode.ManualReset,
        "TraderUpProcessSynchronization", out ProcessCreatedSynchronization);

    private readonly GameDataManager _gameDataManager;
    private readonly ProfileManager _profileManager;
    private readonly TradeManager _tradeManager;
    private readonly InventoryManager _inventoryManager;
    private readonly RoomManager _roomManager;

    private bool ClientConnected { get; set; }
    private IInventoryItem? Furniture { get; set; }
    private IUser? User { get; set; }


    public Extension() : base(new GEarthOptions
    {
        Name = "TraderUp",
        Description = "An extension to up the achievement experienced trader",
        Author = "Tourner",
        Version = Assembly.GetExecutingAssembly().GetVersion(false)
    })
    {
        _gameDataManager = new GameDataManager();
        _profileManager = new ProfileManager(this);
        _roomManager = new RoomManager(this);
        _tradeManager = new TradeManager(this, _profileManager, _roomManager);
        _inventoryManager = new InventoryManager(this);

        _roomManager.Left += HandleLeftRoomManager;
        _tradeManager.Opened += HandleOpenedTradeManager;
        _tradeManager.Updated += HandleUpdatedTradeManager;
    }

    public async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        await RunLongRunningAsync(port, cancellationToken);
        await EnsureHabboConnectedAsync(cancellationToken);
        await LoadGameData(cancellationToken);
        await LoadProfileManagerAsync();
        await EnsureInRoomAsync(cancellationToken);
        await LoadInventoryAsync(cancellationToken);
    }

    public IEnumerable<string> GetUsersToTrade() =>
        _roomManager
            .Room!
            .Users
            .Where(x => x.Id != _profileManager.UserData!.Id)
            .Select(x => x.Name);

    public IEnumerable<string> GetFurnitureToTrade() =>
        _inventoryManager
            .Inventory!
            .Where(x => x.IsTradeable)
            .GroupBy(x => x.GetName())
            .Select(x => x.Key);

    public async Task TradeAsync(string username, string furniture, CancellationToken cancellationToken)
    {
        Logger.Information("User {0} selected to trade with furniture {1}", username, furniture);

        User = _roomManager.Room!.Users.FirstOrDefault(x =>
            x.Name.Equals(username, StringComparison.InvariantCultureIgnoreCase));

        Furniture = _inventoryManager.Inventory!.FirstOrDefault(x =>
            x.GetName().Equals(furniture, StringComparison.InvariantCultureIgnoreCase));

        if (ProcessCreatedSynchronization)
        {
            Synchronization.WaitOne();
        }
        else
        {
            Synchronization.Set();
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1_000));
        do
        {
            if (User is null)
            {
                Logger.Error("User {0} selected to trade left room", username);
                return;
            }

            if (Furniture is null)
            {
                Logger.Error("Furniture {0} selected to trade has run out", furniture);
                return;
            }

            if (!ProcessCreatedSynchronization || _tradeManager.IsTrading)
            {
                continue;
            }

            Logger.Information("Open trading...");
            this.Send(new TradeUserMsg(User.Index));
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private Task RunLongRunningAsync(int port, CancellationToken cancellationToken)
    {
        object? thrownException = null;
        Logger.Information("Starting connection to G-Earth...");
        
        _ = Task.Run(async () =>
        {
            try
            {
                await base.RunAsync(new GEarthConnectOptions(Port: port), cancellationToken);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
        }, cancellationToken);

        var sw = new Stopwatch();
        sw.Start();
        while (sw.Elapsed < TimeSpan.FromMilliseconds(2_500))
        {
            if (thrownException is not null)
            {
                throw new TimeoutException("G-Earth is not open");
            }
        }
        
        Logger.Information("Connected to G-Earth on port {0}", port);

        return Task.CompletedTask;
    }

    private async Task EnsureHabboConnectedAsync(CancellationToken cancellationToken)
    {
        Logger.Information("Awaiting to habbo connection...");

        while (!cancellationToken.IsCancellationRequested && !ClientConnected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    private async Task LoadGameData(CancellationToken cancellationToken)
    {
        Logger.Information("Getting game information from hotel {0}...", Session.Hotel.Name);

        await _gameDataManager.LoadAsync(Session.Hotel, cancellationToken: cancellationToken);

        Logger.Information("Game information loaded");
    }

    private async Task LoadProfileManagerAsync()
    {
        Logger.Information("Getting user information...");

        await _profileManager.GetUserDataAsync();

        Logger.Information("Welcome {0} to [lightgoldenrod3]TraderUp[/]", _profileManager.UserData!.Name);
    }

    private async Task EnsureInRoomAsync(CancellationToken cancellationToken)
    {
        IRoom? lastRoom = null;
        while (true)
        {
            Logger.Information("Enter a room...");

            IRoom? room;
            while (!_roomManager.EnsureInRoom(out room) || room.Equals(lastRoom))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            var data = room.Data!;

            Logger.Information("Entered a room {0} from {1} with {2} users...", data.Name, data.OwnerName, data.Users);

            if (!room.HasUserToTrade(_profileManager.UserData!.Id))
            {
                Logger.Warning("There are no other users in this room");
                lastRoom = room;
                continue;
            }

            break;
        }
    }

    private async Task LoadInventoryAsync(CancellationToken cancellationToken)
    {
        Logger.Information("Loading inventory...");

        await _inventoryManager.GetInventoryAsync(cancellationToken: cancellationToken);

        Logger.Information("Inventory loaded");
    }

    protected override void OnConnected(ConnectedEventArgs e)
    {
        base.OnConnected(e);
        ClientConnected = true;
        var client = e.Session.Client;
        var hotel = e.Session.Hotel;
        Logger.Information("Connected to the Hotel {0}, Client {1} - Version {2}", hotel.Name, client.Version,
            client.Identifier);
    }

    protected override void OnDisconnected()
    {
        base.OnDisconnected();
        throw new LeftException("Connection closed");
    }
};
