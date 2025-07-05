using ElectricalProgressive.Content.Block.EGenerator;
using ElectricalProgressive.Content.Block.ETermoGenerator;
using ElectricalProgressive.Interface;
using ElectricalProgressive.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ElectricalProgressive.Content.Block.EConnector
{
    public class BEBehaviorEConnector : BlockEntityBehavior, IElectricConductor
    {
        public BEBehaviorEConnector(BlockEntity blockentity) : base(blockentity)
        {
        }

        public new BlockPos Pos => Blockentity.Pos;




        /// <summary>
        /// Обновление блока кабеля
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {

        }


    }
}
