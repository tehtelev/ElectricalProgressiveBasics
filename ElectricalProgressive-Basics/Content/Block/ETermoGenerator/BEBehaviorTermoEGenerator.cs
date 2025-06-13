using System;
using System.Linq;
using System.Text;
using ElectricalProgressive.Content.Block.EGenerator;
using ElectricalProgressive.Interface;
using ElectricalProgressive.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace ElectricalProgressive.Content.Block.ETermoGenerator;

public class BEBehaviorTermoEGenerator : BlockEntityBehavior, IElectricProducer
{
    protected float PowerOrder;           // Просят столько энергии (сохраняется)
    public const string PowerOrderKey = "electricalprogressive:powerOrder";

    protected float PowerGive;           // Отдаем столько энергии (сохраняется)
    public const string PowerGiveKey = "electricalprogressive:powerGive";


    //protected bool IsBurned => Block.Variant["type"] == "burned";
    protected bool IsBurned => false;

    

    public new BlockPos Pos => Blockentity.Pos;


    public BEBehaviorTermoEGenerator(BlockEntity blockEntity) : base(blockEntity)
    {

    }



    public void Update()
    {
        //смотрим надо ли обновить модельку когда сгорает прибор
        if (Api.World.BlockAccessor.GetBlockEntity(Blockentity.Pos) is BlockEntityETermoGenerator
            {
                AllEparams: not null
            } entity)
        {
            var hasBurnout = entity.AllEparams.Any(e => e.burnout);
            if (hasBurnout)
                ParticleManager.SpawnBlackSmoke(Api.World, Pos.ToVec3d().Add(0.5, 0.5, 0.5));

            /*
            if (hasBurnout && entity.Block.Variant["type"] != "burned")
            {
                var type = "type";
                var variant = "burned";

                Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant(type, variant)).BlockId, Pos);
            }
            */
        }

        //Blockentity.MarkDirty(true); //обновлять здесь уже лишнее
    }



    public float Produce_give()
    {
        BlockEntityETermoGenerator? entity = null;
        if (Blockentity is BlockEntityETermoGenerator temp)
        {
            entity = temp;
            if (temp.GenTemp > 20)
            {
                PowerGive = Math.Min(PowerOrder, temp.Power);
            }
            else
                PowerGive = 0;

        }

        return PowerGive;

    }



    public void Produce_order(float amount)
    {
        PowerOrder = amount;
    }



    public float getPowerGive() => PowerGive;


    public float getPowerOrder() => PowerOrder;



    /// <summary>
    /// Подсказка при наведении на блок
    /// </summary>
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder)
    {
        base.GetBlockInfo(forPlayer, stringBuilder);

        if (Api.World.BlockAccessor.GetBlockEntity(Blockentity.Pos) is not BlockEntityETermoGenerator entity)
            return;

        if (IsBurned)
        {
            stringBuilder.AppendLine(Lang.Get("Burned"));
            return;
        }

        stringBuilder.AppendLine(StringHelper.Progressbar(Math.Min(PowerGive, PowerOrder) / entity.Power * 100));
        stringBuilder.AppendLine("└ " + Lang.Get("Production") + ": " + Math.Min(PowerGive, PowerOrder) + "/" + entity.Power + " " + Lang.Get("W"));
    }



    /// <summary>
    /// Сохранение параметров в дерево атрибутов
    /// </summary>
    /// <param name="tree"></param>
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat(PowerOrderKey, PowerOrder);
        tree.SetFloat(PowerGiveKey, PowerGive);
    }


    /// <summary>
    /// Загрузка параметров из дерева атрибутов
    /// </summary>
    /// <param name="tree"></param>
    /// <param name="worldAccessForResolve"></param>
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        PowerOrder = tree.GetFloat(PowerOrderKey);
        PowerGive = tree.GetFloat(PowerGiveKey);
    }
}
