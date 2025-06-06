﻿using ElectricalProgressive.Utils;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace ElectricalProgressive.Content.Block.EGenerator;

public class BlockEntityEGenerator : BlockEntityEBase
{
    public Facing Facing = Facing.None;
    public const string FacingKey = "electricalprogressive:facing";

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetBytes(FacingKey, SerializerUtil.Serialize(Facing));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        try
        {
            Facing = SerializerUtil.Deserialize<Facing>(tree.GetBytes(FacingKey));
        }
        catch (Exception exception)
        {
            Api?.Logger.Error(exception.ToString());
        }
    }
}
