using ElectricalProgressive.Interface;
using ElectricalProgressive.Utils;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;


namespace ElectricalProgressive.Content.Block.ETransformator;

public class BEBehaviorETransformator : BlockEntityBehavior, IElectricTransformator
{
    float maxCurrent; //максимальный ток
    float power;      //мощность

    public BEBehaviorETransformator(BlockEntity blockEntity) : base(blockEntity)
    {
        maxCurrent = MyMiniLib.GetAttributeFloat(this.Block, "maxCurrent", 5.0F);
    }

    public bool isBurned => this.Block.Variant["state"] == "burned";
    public new BlockPos Pos => this.Blockentity.Pos;

    public int highVoltage => MyMiniLib.GetAttributeInt(this.Block, "voltage", 32);

    public int lowVoltage => MyMiniLib.GetAttributeInt(this.Block, "lowVoltage", 32);



    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder)
    {
        base.GetBlockInfo(forPlayer, stringBuilder);

        //проверяем не сгорел ли прибор
        if (this.Api.World.BlockAccessor.GetBlockEntity(this.Blockentity.Pos) is not BlockEntityETransformator entity)
            return;

        if (isBurned)
        {
            // выясняем причину сгорания (надо куда-то вынести сей кусочек)
            string cause = "";
            if (entity.AllEparams.Any(e => e.causeBurnout == 1))
            {
                cause = ElectricalProgressiveBasics.causeBurn[1];
            }
            else if (entity.AllEparams.Any(e => e.causeBurnout == 2))
            {
                cause = ElectricalProgressiveBasics.causeBurn[2];
            }
            else if (entity.AllEparams.Any(e => e.causeBurnout == 3))
            {
                cause = ElectricalProgressiveBasics.causeBurn[3];
            }

            stringBuilder.AppendLine(Lang.Get("Burned") + " " + cause);
            return;
        }

        //stringBuilder.AppendLine(StringHelper.Progressbar(getPower() / (lowVoltage * maxCurrent) * 100));
        //stringBuilder.AppendLine("└ " + Lang.Get("Power") + ": " + getPower() + " / " + lowVoltage * maxCurrent + " " + Lang.Get("W"));
        stringBuilder.AppendLine("└ " + Lang.Get("Power") + ": " + ((int)getPower()).ToString() + " " + Lang.Get("W"));
        stringBuilder.AppendLine();
    }


    public void Update()
    {
        //смотрим надо ли обновить модельку когда сгорает трансформатор
        if (this.Api.World.BlockAccessor.GetBlockEntity(this.Blockentity.Pos) is BlockEntityETransformator
            {
                AllEparams: not null
            } entity)
        {
            bool hasBurnout = entity.AllEparams.Any(e => e.burnout);
            if (hasBurnout)
            {
                ParticleManager.SpawnBlackSmoke(this.Api.World, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
            if (hasBurnout && entity.Block.Variant["state"] != "burned")
            {
                this.Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("state", "burned")).BlockId, Pos);
            }
        }

        this.Blockentity.MarkDirty(true);
    }


    public float getPower()
    {
        return this.power;
    }

    public void setPower(float power)
    {
        this.power = power;
    }
}
