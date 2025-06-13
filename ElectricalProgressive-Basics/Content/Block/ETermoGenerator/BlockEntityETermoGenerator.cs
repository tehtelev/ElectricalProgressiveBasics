using System;
using ElectricalProgressive;
using ElectricalProgressive.Content.Block;
using ElectricalProgressive.Content.Block.Termoplastini;
using ElectricalProgressive.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ElectricalProgressive.Content.Block.ETermoGenerator;

public class BlockEntityETermoGenerator : BlockEntityGenericTypedContainer, IHeatSource
{
   
    private Facing facing = Facing.None;

    private BEBehaviorElectricalProgressive? ElectricalProgressive => GetBehavior<BEBehaviorElectricalProgressive>();

    public Facing Facing
    {
        get => this.facing;
        set
        {
            if (value != this.facing)
            {
                this.ElectricalProgressive.Connection =
                    FacingHelper.FullFace(this.facing = value);
            }
        }
    }

    //передает значения из Block в BEBehaviorElectricalProgressive
    public (EParams, int) Eparams
    {
        get => this.ElectricalProgressive?.Eparams ?? (new EParams(), 0);
        set => this.ElectricalProgressive!.Eparams = value;
    }

    //передает значения из Block в BEBehaviorElectricalProgressive
    public EParams[] AllEparams
    {
        get => this.ElectricalProgressive?.AllEparams ?? new EParams[]
                    {
                        new EParams(),
                        new EParams(),
                        new EParams(),
                        new EParams(),
                        new EParams(),
                        new EParams()
                    };
        set
        {
            if (this.ElectricalProgressive != null)
            {
                this.ElectricalProgressive.AllEparams = value;
            }
        }
    }




    ICoreClientAPI capi;
    ICoreServerAPI sapi;
    private InventoryTermoGenerator inventory;
    private GuiBlockEntityETermoGenerator clientDialog;

    
    private float prevGenTemp = 20f;
    public float genTemp = 20f;

    /// <summary>
    /// Коэффициенты КПД в зависимости от высоты пластин
    /// </summary>
    public static readonly float[] kpdPerHeight =
{
        0.15F, // 1-й 
        0.14F, // 2-й 
        0.13F, // 3-й 
        0.12F, // 4-й 
        0.11F, // 5-й 
        0.09F, // 6-й 
        0.08F, // 7-й 
        0.07F, // 8-й 
        0.06F, // 9-й 
        0.05F  // 10-й 
    };

    /// <summary>
    /// Максимальная температура топлива
    /// </summary>
    private int maxTemp;

    /// <summary>
    /// Текущее время горения топлива
    /// </summary>
    private float fuelBurnTime;

    /// <summary>
    /// Максимальное время горения топлива
    /// </summary>
    private float maxBurnTime;

    /// <summary>
    /// Температура в генераторе
    /// </summary>
    public float GenTemp => genTemp;


    /// <summary>
    /// Собственно выходная максимальная мощность
    /// </summary>
    public float Power
    {
        get
        {
            var envTemp = EnvironmentTemperature(); //температура окружающей среды
            if (kpd > 0)
            {
                if (genTemp <= envTemp) //окружающая среда теплее? 
                {
                    return 1f;
                }
                else
                {
                    return (genTemp - envTemp) * kpd / 2.0F;  //учитываем разницу температур с окружающей средой и КПД
                }
            }
            else
                return 1f;
        }
    }
    
    /// <summary>
    /// КПД генератора в долях
    /// </summary>
    private float kpd=0f;

    /// <summary>
    /// Горизонтальные направления для смещения
    /// </summary>
    private static readonly BlockFacing[] offsets_horizontal = BlockFacing.HORIZONTALS;


    private ItemSlot FuelSlot => this.inventory[0];
    public ItemStack FuelStack
    {
        get { return this.inventory[0].Itemstack; }
        set
        {
            this.inventory[0].Itemstack = value;
            this.inventory[0].MarkDirty();
        }
    }

    public override InventoryBase Inventory => inventory;

    public string DialogTitle => Lang.Get("termogen");

    public override string InventoryClassName => "termogen";

    public BlockEntityETermoGenerator()
    {
        this.inventory = new InventoryTermoGenerator(null, null);
        this.inventory.SlotModified += OnSlotModified;
    }


    /// <summary>
    /// Инициализация блока
    /// </summary>
    /// <param name="api"></param>
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api.Side == EnumAppSide.Server)
        {
            sapi = api as ICoreServerAPI;
        }
        else
        {
            capi = api as ICoreClientAPI;
        }

        this.inventory.Pos = this.Pos;
        this.inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);

        this.RegisterGameTickListener(new Action<float>(OnBurnTick), 1000);

        CanDoBurn();
    }


    /// <summary>
    /// При ломании блока
    /// </summary>
    /// <param name="byPlayer"></param>
    public override void OnBlockBroken(IPlayer byPlayer = null)
    {
        base.OnBlockBroken(null);
    }




    /// <summary>
    /// Отвечает за тепло отдаваемое в окружающую среду
    /// </summary>
    /// <param name="world"></param>
    /// <param name="heatSourcePos"></param>
    /// <param name="heatReceiverPos"></param>
    /// <returns></returns>
    public float GetHeatStrength(
      IWorldAccessor world,
      BlockPos heatSourcePos,
      BlockPos heatReceiverPos)
    {
        return Math.Max((float)(((float)this.genTemp - 20.0F) / ((float)1300F - 20.0F) * MyMiniLib.GetAttributeFloat(this.Block, "maxHeat", 0.0F)), 0.0f);
    }


    /// <summary>
    /// Получает температуру окружающей среды
    /// </summary>
    /// <returns></returns>
    protected virtual int EnvironmentTemperature()
    {
        return (int)this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, this.Api.World.Calendar.TotalDays).Temperature;
    }




    public void OnSlotModified(int slotId)
    {
        if (slotId == 0)
        {
            if (Inventory[0].Itemstack != null && !Inventory[0].Empty &&
                Inventory[0].Itemstack.Collectible.CombustibleProps != null)
            {
                if (fuelBurnTime == 0)
                    CanDoBurn();
            }
        }

        base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
        this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);

        if (this.Api is ICoreClientAPI && this.clientDialog != null)
        {
            clientDialog.Update(genTemp, fuelBurnTime);
        }

        IWorldChunk chunkatPos = this.Api.World.BlockAccessor.GetChunkAtBlockPos(this.Pos);
        if (chunkatPos == null)
            return;

        chunkatPos.MarkModified();
    }




    public void OnBurnTick(float deltatime)
    {
        Calculate_kpd();

        if (this.Api is ICoreServerAPI)
        {
            if (fuelBurnTime > 0f)
            {
                genTemp = ChangeTemperature(genTemp, maxTemp, deltatime);
                fuelBurnTime -= deltatime; // burn!
                if (fuelBurnTime <= 0f)
                {
                    fuelBurnTime = 0f;
                    maxBurnTime = 0f;
                    maxTemp = 20; // important
                    if (!Inventory[0].Empty)
                        CanDoBurn();
                }
            }
            else
            {
                if (genTemp != 20f)
                    genTemp = ChangeTemperature(genTemp, 20f, deltatime);
                CanDoBurn();
            }

            

            MarkDirty(true, null);
        }

       

        // обновляем диалоговое окно на клиенте
        if (this.Api != null && this.Api.Side == EnumAppSide.Client)
        {
            if (this.clientDialog != null)
                clientDialog.Update(genTemp, fuelBurnTime);

        }
  
    }



    /// <summary>
    /// Расчет КПД генератора
    /// </summary>
    public void Calculate_kpd()
    {
        // Получаем доступ к блочным данным один раз
        var accessor = this.Api.World.BlockAccessor;
        kpd = 0f;

        // Перебираем потенциальные термопластины по высоте
        for (int level = 1; level <= 10; level++)
        {
            // Получаем позицию и блок термопластины
            var platePos = Pos.UpCopy(level);
            if (accessor.GetBlock(platePos) is not BlockTermoplastini)
                break;

            // Проверяем соседние блоки и считаем количество воздухом незаполненных сторон
            float airSides = 0f;
            foreach (var face in offsets_horizontal)
            {
                var neighBlock = accessor.GetBlock(platePos.AddCopy(face));
                if (neighBlock != null && neighBlock.BlockId == 0)
                {
                    airSides += 1f;
                }
            }

            // Учитываем множитель КПД на данном уровне
            // 0.25f — вклад одной стороны в КПД
            kpd += airSides * 0.25f * kpdPerHeight[level - 1];
        }
    }




    /// <summary>
    /// Проверяет, можно ли сжечь топливо в генераторе
    /// </summary>
    public void CanDoBurn()
    {
        CombustibleProperties fuelProps = FuelSlot.Itemstack?.Collectible.CombustibleProps;
        if (fuelProps == null)
            return;

        if (fuelBurnTime > 0)
            return;

        if (fuelProps.BurnTemperature > 0f && fuelProps.BurnDuration > 0f)
        {
            maxBurnTime = fuelBurnTime = fuelProps.BurnDuration;
            maxTemp = fuelProps.BurnTemperature;
            FuelStack.StackSize--;
            if (FuelStack.StackSize <= 0)
            {
                FuelStack = null;
            }

            FuelSlot.MarkDirty();
            MarkDirty(true);
        }
    }


    /// <summary>
    /// Изменяет температуру в зависимости от времени и разницы температур
    /// </summary>
    /// <param name="fromTemp"></param>
    /// <param name="toTemp"></param>
    /// <param name="deltaTime"></param>
    /// <returns></returns>
    public float ChangeTemperature(float fromTemp, float toTemp, float deltaTime)
    {
        float diff = Math.Abs(fromTemp - toTemp);
        deltaTime += deltaTime * (diff / 28f);
        if (diff < deltaTime)
        {
            return toTemp;
        }

        if (fromTemp > toTemp)
        {
            deltaTime = -deltaTime;
        }

        if (Math.Abs(fromTemp - toTemp) < 1f)
        {
            return toTemp;
        }
        return fromTemp + deltaTime;
    }








    /// <summary>
    /// Обработчик нажатия правой кнопкой мыши по блоку, открывает диалоговое окно
    /// </summary>
    /// <param name="byPlayer"></param>
    /// <param name="blockSel"></param>
    /// <returns></returns>
    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        // меняем состояние дверцы
        if (this.Api != null)
        {
            if (this.Block.Variant["state"] == "open")
            {
                var originalBlock = this.Api.World.BlockAccessor.GetBlock(Pos);
                var newBlockAL = originalBlock.CodeWithVariant("state", "closed");
                var newBlock = this.Api.World.GetBlock(newBlockAL);
                this.Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
                MarkDirty();
            }
            else if (this.Block.Variant["state"] == "closed")
            {
                var originalBlock = this.Api.World.BlockAccessor.GetBlock(Pos);
                var newBlockAL = originalBlock.CodeWithVariant("state", "open");
                var newBlock = this.Api.World.GetBlock(newBlockAL);
                this.Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
                MarkDirty();
            }
        }

        // открываем диалоговое окно
        if (this.Api.Side == EnumAppSide.Client)
        {
            base.toggleInventoryDialogClient(byPlayer, delegate
            {
                this.clientDialog =
                    new GuiBlockEntityETermoGenerator(DialogTitle, Inventory, this.Pos, this.capi, this);
                clientDialog.Update(genTemp, fuelBurnTime);
                return this.clientDialog;
            });
        }
        return true;
    }


    /// <summary>
    /// При установке блока, устанавливает соединение электричества
    /// </summary>
    /// <param name="byItemStack"></param>
    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        var electricity = ElectricalProgressive;
        if (electricity != null)
        {
            electricity.Connection = Facing.DownAll;
        }
    }


    /// <summary>
    /// При удалении блока, закрывает диалоговое окно и отключает электричество
    /// </summary>
    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        this.clientDialog?.TryClose();
        var electricity = ElectricalProgressive;

        if (electricity != null)
        {
            electricity.Connection = Facing.None;
        }
    }



    /// <summary>
    /// Сохраняет атрибуты
    /// </summary>
    /// <param name="tree"></param>
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        ITreeAttribute invtree = new TreeAttribute();
        this.inventory.ToTreeAttributes(invtree);
        tree["inventory"] = invtree;
        tree.SetFloat("genTemp", genTemp);
        tree.SetInt("maxTemp", maxTemp);
        tree.SetFloat("fuelBurnTime", fuelBurnTime);
        tree.SetBytes("electricalprogressive:facing", SerializerUtil.Serialize(this.facing));
    }


    /// <summary>
    /// Загружает атрибуты 
    /// </summary>
    /// <param name="tree"></param>
    /// <param name="worldForResolving"></param>
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        this.inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        if (Api != null)
            Inventory.AfterBlocksLoaded(this.Api.World);
        genTemp = tree.GetFloat("genTemp", 0);
        maxTemp = tree.GetInt("maxTemp", 0);
        fuelBurnTime = tree.GetFloat("fuelBurnTime", 0);

        if (Api != null && Api.Side == EnumAppSide.Client)
        {
            if (this.clientDialog != null)
                clientDialog.Update(genTemp, fuelBurnTime);
            MarkDirty(true, null);
        }

        try
        {
            this.facing = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricalprogressive:facing"));
        }
        catch (Exception exception)
        {
            this.Api?.Logger.Error(exception.ToString());
        }
    }
    
}