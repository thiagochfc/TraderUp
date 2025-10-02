using Xabbo;
using Xabbo.Core.Game;

namespace TraderUp;

public static class RoomManagerExtensions
{
    public static bool HasUserToTrade(this IRoom room, Id currentUser) =>
        room.Users.Any(x => x.Id != currentUser);
}
