﻿using L2Dn.AuthServer.Model;
using L2Dn.AuthServer.NetworkGameServer.IncomingPackets;
using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.AuthServer.NetworkGameServer;

internal sealed class GameServerPacketHandler: PacketHandler<GameServerSession, GameServerSessionState>
{
    public GameServerPacketHandler()
    {
        RegisterPacket<RegisterGameServerPacket>(IncomingPacketCodes.RegisterGameServer);
        RegisterPacket<PingRequestPacket>(IncomingPacketCodes.PingRequest);
    }

    public override ValueTask OnDisconnectedAsync(Connection<GameServerSession> connection)
    {
        GameServerInfo? serverInfo = connection.Session.ServerInfo;
        if (serverInfo is not null)
        {
            serverInfo.Connection = null;
            serverInfo.IsOnline = false;
        }
        
        return base.OnDisconnectedAsync(connection);
    }
}