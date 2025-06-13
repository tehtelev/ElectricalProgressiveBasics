using ElectricalProgressive.Interface;
using Vintagestory.API.Common;

namespace ElectricalProgressive.Content.Block.EWoodcutter;

public class BEBehaviorEWoodcutter : BEBehaviorBase, IElectricConsumer
{
    public BEBehaviorEWoodcutter(BlockEntity blockentity) : base(blockentity)
    {
    }

    public float Consume_request()
    {
        return 0;
    }

    public void Consume_receive(float amount)
    {

    }

    public void Update()
    {

    }

    public float getPowerReceive()
    {
        return 0;
    }

    public float getPowerRequest()
    {
        return 0;
    }
}