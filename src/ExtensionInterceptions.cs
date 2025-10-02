using Xabbo;
using Xabbo.Core;
using Xabbo.Core.Events;
using Xabbo.Core.Messages.Incoming;
using Xabbo.Core.Messages.Outgoing;
using Xabbo.Messages.Flash;

namespace TraderUp;

internal partial class Extension
{
    private void HandleLeftRoomManager()
    {
        if (User is null && Furniture is null)
        {
            return;
        }
        
        throw new LeftException("You left room");
    }
    
    private void HandleOpenedTradeManager(TradeOpenedEventArgs e)
    {
        Logger.Information("Trade opened");

        if (!e.IsInitiator && e.Partner.Id != User!.Id)
        {
            _ = Task.Run(() =>
            {
                this.Send(new CloseTradeMsg());
            });
        }
        
        _ = Task.Run(() =>
        {
            Logger.Information("Offer item to trade");
            this.Send(new OfferTradeItemsMsg([Furniture!]));
        });
    }

    private void HandleUpdatedTradeManager(TradeUpdatedEventArgs e)
    {
        if (e.PartnerOffer.FurniCount <= 0 || e.SelfOffer.FurniCount <= 0)
        {
            return;
        }
        
        Logger.Information("Accepting trade...");
        this.Send(Out.AcceptTrading);
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(3_150));
            this.Send(Out.ConfirmAcceptTrading);
            Logger.Information("Trading confirmed");
        });
    }

    [InterceptIn(nameof(In.TradingItemList))]
    private void HandleTradingItemList(Intercept e)
    {
        var message = e.Packet.Read<TradeOffersMsg>();

        if (message.First.FurniCount <= 1 && message.Second.FurniCount <= 1)
        {
            return;
        }

        Logger.Information("Item offered to trade");
    }
    
    [InterceptIn(nameof(In.FurniListAddOrUpdate))]
    private void HandleFurniListAddOrUpdate(Intercept e)
    {
        var message = e.Packet.Read<InventoryItem>();
        
        // In trading, Habbo utilizes this field to identify the furniture, and it needs to be negative
        // In this incoming packet, this field is sent with a value of 1
        message.ItemId = message.Id > 0 ? message.Id * -1 : message.Id;
        
        Furniture = message;
    }
    
    [InterceptIn(nameof(In.UserRemove))]
    private void HandleUserRemove(Intercept e)
    {
        var value = e.Packet.Read<string>();

        if (!int.TryParse(value, out int index))
        {
            return;
        }

        if (User?.Index == index)
        {
            User = null;
        }
    }
}
