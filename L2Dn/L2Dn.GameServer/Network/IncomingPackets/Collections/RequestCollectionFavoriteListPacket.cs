﻿using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Network.OutgoingPackets.Collections;
using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets.Collections;

public struct RequestCollectionFavoriteListPacket: IIncomingPacket<GameSession>
{
    public void ReadContent(PacketBitReader reader)
    {
        //reader.ReadByte(); // unknown
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        Player? player = session.Player;
        if (player == null)
            return ValueTask.CompletedTask;

        player.sendPacket(new ExCollectionFavoriteListPacket());
        
        return ValueTask.CompletedTask;
    }
}