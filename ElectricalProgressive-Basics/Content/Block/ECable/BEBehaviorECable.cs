using ElectricalProgressive.Content.Block.ETermoGenerator;
using ElectricalProgressive.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ElectricalProgressive.Content.Block.ECable
{
    public class BEBehaviorECable : BlockEntityBehavior
    {
        public BEBehaviorECable(BlockEntity blockentity) : base(blockentity)
        {
        }



        /// <summary>
        /// Подсказка при наведении на блок
        /// </summary>
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder)
        {
            base.GetBlockInfo(forPlayer, stringBuilder);

            if (Api.World.BlockAccessor.GetBlockEntity(Blockentity.Pos) is not BlockEntityECable entity)
                return;



            //stringBuilder.AppendLine("Заглушка");
            
        }

    }
}
