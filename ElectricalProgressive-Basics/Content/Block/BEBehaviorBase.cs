using Vintagestory.API.Common;

namespace ElectricalProgressive.Content.Block;

public abstract class BEBehaviorBase : BlockEntityBehavior
{
    public bool IsBurned => this.Block.Variant["state"] == "burned";

    protected BEBehaviorBase(BlockEntity blockentity) : base(blockentity)
    {
    }
}