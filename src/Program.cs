using System.Diagnostics;
using System.Reflection;
using Serilog;
using Serilog.Sinks.Spectre;
using Spectre.Console;
using TraderUp;

Console.Title = "TraderUp";
using CancellationTokenSource cts = new();

// Apply configurations
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Spectre("{Timestamp:HH:mm:ss} [{Level:u4}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

WriteTitle();

var extension = new Extension();

try
{
    int port = GetPort();
    
    await AnsiConsole
        .Status()
        .StartAsync("[lightgoldenrod3]Initializating extension[/]", async _ =>
        {
            await extension.RunAsync(port, cts.Token);
        });

    var userSelected = await AnsiConsole.PromptAsync(
        new SelectionPrompt<string>()
            .Title("Which user do you want to trade with?")
            .PageSize(5)
            .MoreChoicesText("[grey](Move up and down to reveal more users)[/]")
            .AddChoices(extension.GetUsersToTrade()), cts.Token
    );

    var furniture = await AnsiConsole.PromptAsync(
        new SelectionPrompt<string>()
            .Title("Which furniture do you want to trade with?")
            .PageSize(5)
            .MoreChoicesText("[grey](Move up and down to reveal more furniture)[/]")
            .AddChoices(extension.GetFurnitureToTrade()), cts.Token
    );

    await AnsiConsole
        .Status()
        .StartAsync("[lightgoldenrod3]Trading...[/]", async _ =>
        {
            await extension.TradeAsync(userSelected, furniture, cts.Token);
        });
}
catch (Exception ex)
{
    if (ex is LeftException or TimeoutException)
    {
        Log.Fatal(ex.Message);
    }
    else
    {
        Log.Error(ex,
            "Something went wrong, copy the message below and click here [link]https://github.com/thiagochfc/TraderUp/issues/new[/]\n" +
            "Put the message in \"Add a description\"");
    }

    Console.Write("Press any key to exit...");
    Console.ReadLine();
}

return 0;

void WriteTitle()
{
    AnsiConsole.Write(new FigletText("TraderUp").Centered().Color(Color.LightGoldenrod3));
    AnsiConsole.WriteLine();
    Rule rule = new()
    {
        Title = $"[lightgoldenrod2_1]Developed by Tourner {Assembly.GetExecutingAssembly().GetVersion()}[/]",
        Style = Style.Parse("LightGoldenrod3"),
        Justification = Justify.Right
    };
    AnsiConsole.Write(rule);
    AnsiConsole.WriteLine();
}

static int GetPort()
{
    int port = 9092;
    var quantityProcessOpened = Process.GetProcessesByName("TraderUp").Length;
    switch (quantityProcessOpened)
    {
        case > 2:
            Log.Error("Maximum processes opened");
            Environment.Exit(0);
            break;
        case > 1:
            Log.Information("There is already an extension using the port {0}...", port);
            port++;
            break;
    }

    return port;
}
