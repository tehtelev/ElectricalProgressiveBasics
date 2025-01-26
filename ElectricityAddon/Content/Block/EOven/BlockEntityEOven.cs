﻿using System;
using System.Text;
using Electricity.Utils;
using ElectricityAddon.Content.Block.EStove;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ElectricityAddon.Content.Block.EOven;
public class BlockEntityEOven : BlockEntityDisplay, IHeatSource
{
    public static int BakingStageThreshold = 100;
    public const int maxBakingTemperatureAccepted = 260;
    private bool burning;
    private bool clientSidePrevBurning;
    public float prevOvenTemperature = 20f;
    public float ovenTemperature = 20f;
    private readonly OvenItemData[] bakingData;
    private ItemStack lastRemoved;
    private int rotationDeg;
    private Random prng;
    //private int syncCount;
    //private ILoadedSound ambientSound;
    internal InventoryEOven ovenInv;


    public virtual float maxTemperature => 300f;

    public virtual int bakeableCapacity => 4;

    public virtual int fuelitemCapacity => 6;
    private Electricity.Content.Block.Entity.Behavior.Electricity? Electricity => GetBehavior<Electricity.Content.Block.Entity.Behavior.Electricity>();

    private EnumOvenContentMode OvenContentMode
    {
        get
        {
            ItemSlot firstNonEmptySlot = this.ovenInv.FirstNonEmptySlot;
            if (firstNonEmptySlot == null)
                return EnumOvenContentMode.Firewood;
            BakingProperties bakingProperties = BakingProperties.ReadFrom(firstNonEmptySlot.Itemstack);
            if (bakingProperties == null)
                return EnumOvenContentMode.Firewood;
            return !bakingProperties.LargeItem ? EnumOvenContentMode.Quadrants : EnumOvenContentMode.SingleCenter;
        }
    }

    public BlockEntityEOven()
    {
        this.bakingData = new OvenItemData[this.bakeableCapacity];
        for (int index = 0; index < this.bakeableCapacity; ++index)
            this.bakingData[index] = new OvenItemData();
        this.ovenInv = new InventoryEOven("eoven-0", this.bakeableCapacity);
    }

    public override InventoryBase Inventory => (InventoryBase)this.ovenInv;

    public override string InventoryClassName => "eoven";

    public ItemSlot FuelSlot => this.ovenInv[0];

    public bool IsBurning;

    public override void Initialize(ICoreAPI api)
    {
        this.capi = api as ICoreClientAPI;
        base.Initialize(api);
        this.ovenInv.LateInitialize(this.InventoryClassName + "-" + this.Pos?.ToString(), api);
        this.RegisterGameTickListener(new Action<float>(this.OnBurnTick), 100);
        this.prng = new Random(this.Pos.GetHashCode());
        this.SetRotation();
    }

    private void SetRotation()
    {
        switch (this.Block.Variant["side"])
        {
            case "south":
                this.rotationDeg = 270;
                break;
            case "west":
                this.rotationDeg = 180;
                break;
            case "east":
                this.rotationDeg = 0;
                break;
            default:
                this.rotationDeg = 90;
                break;
        }
    }

    public virtual bool OnInteract(IPlayer byPlayer, BlockSelection bs)
    {
        ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeHotbarSlot.Empty)                    //если слот пустой - пробуем брать
        {
            if (!this.TryTake(byPlayer))
                return false;
            byPlayer.InventoryManager.BroadcastHotbarSlot();
            return true;
        }
        CollectibleObject collectible = activeHotbarSlot.Itemstack.Collectible;        

        if (collectible.Attributes?["bakingProperties"] == null)
        {
            CombustibleProperties combustibleProps = collectible.CombustibleProps;
            if ((combustibleProps != null ? (combustibleProps.SmeltingType == EnumSmeltType.Bake ? 1 : 0) : 0) == 0 || collectible.CombustibleProps.MeltingPoint >= 260)
            {
                if (!this.TryTake(byPlayer))
                    return false;
                byPlayer.InventoryManager.BroadcastHotbarSlot();
                return true;
            }
        }
        if (activeHotbarSlot.Itemstack.Equals(this.Api.World, this.lastRemoved, GlobalConstants.IgnoredStackAttributes) && !this.ovenInv[0].Empty)
        {
            if (this.TryTake(byPlayer))
            {
                byPlayer.InventoryManager.BroadcastHotbarSlot();
                return true;
            }
        }
        else
        {
            AssetLocation code = activeHotbarSlot.Itemstack?.Collectible.Code;
            if (this.TryPut(activeHotbarSlot))
            {
                AssetLocation place = activeHotbarSlot.Itemstack?.Block?.Sounds?.Place;
                this.Api.World.PlaySoundAt(place != (AssetLocation)null ? place : new AssetLocation("sounds/player/buildhigh"), (Entity)byPlayer.Entity, byPlayer, true, 16f, 1f);
                byPlayer.InventoryManager.BroadcastHotbarSlot();
                this.Api.World.Logger.Audit("{0} Put 1-4x{1} into Clay oven at {2}.", (object)byPlayer.PlayerName, (object)code, (object)this.Pos);
                return true;
            }
            if (this.Api is ICoreClientAPI api && activeHotbarSlot.Itemstack?.Block?.GetBehavior<BlockBehaviorCanIgnite>() == null)
            {
                if (activeHotbarSlot.Empty || !activeHotbarSlot.Itemstack.Attributes.GetBool("bakeable", true))
                    api.TriggerIngameError((object)this, "notbakeable", Lang.Get("This item is not bakeable."));
                else if (api != null && !activeHotbarSlot.Empty)
                    capi.TriggerIngameError((object)this, "notbakeable", Lang.Get("Put-into-4-items"));
                return true;
            }
        }
        return false;
    }

    protected virtual bool TryPut(ItemSlot slot)
    {
        BakingProperties bakingProperties1 = BakingProperties.ReadFrom(slot.Itemstack);
        if (bakingProperties1 == null || !slot.Itemstack.Attributes.GetBool("bakeable", true) || bakingProperties1.LargeItem && !this.ovenInv.Empty)
            return false;

        if (slot.Itemstack.StackSize<4 & !bakingProperties1.LargeItem)   //если айтемы в стаке меньше 4 - выход
            return false;

        for (int index = 0; index < this.bakeableCapacity; ++index)
        {
            if (this.ovenInv[index].Empty)
            {
                int num = slot.TryPutInto(this.Api.World, this.ovenInv[index]);
                if (num > 0)
                {
                    this.bakingData[index] = new OvenItemData(this.ovenInv[index].Itemstack);
                    this.updateMesh(index);
                    this.MarkDirty(true);
                    this.lastRemoved = (ItemStack)null;
                }
                
            }
            if (index == 0)
            {
                BakingProperties bakingProperties2 = BakingProperties.ReadFrom(this.ovenInv[0].Itemstack);
                if (bakingProperties2 != null && bakingProperties2.LargeItem)            //если уже лежит пирог - выход
                    return false;
            }
        }
        return true;
    }

    protected virtual bool TryTake(IPlayer byPlayer)
    {
        for (int bakeableCapacity = this.bakeableCapacity; bakeableCapacity >= 0; --bakeableCapacity)
        {
            if (!this.ovenInv[bakeableCapacity].Empty)
            {
                ItemStack itemstack = this.ovenInv[bakeableCapacity].TakeOut(1);
                this.lastRemoved = itemstack == null ? (ItemStack)null : itemstack.Clone();
                if (byPlayer.InventoryManager.TryGiveItemstack(itemstack))
                {
                    AssetLocation place = itemstack.Block?.Sounds?.Place;
                    this.Api.World.PlaySoundAt(place != (AssetLocation)null ? place : new AssetLocation("sounds/player/throw"), (Entity)byPlayer.Entity, byPlayer, true, 16f, 1f);
                }
                if (itemstack.StackSize > 0)
                    this.Api.World.SpawnItemEntity(itemstack, this.Pos);
                this.Api.World.Logger.Audit("{0} Took 1x{1} from Clay oven at {2}.", (object)byPlayer.PlayerName, (object)itemstack.Collectible.Code, (object)this.Pos);
                this.bakingData[bakeableCapacity].CurHeightMul = 1f;
                this.updateMesh(bakeableCapacity);
                this.MarkDirty(true);
                return true;
            }
        }
        return false;
    }
    
    public float GetHeatStrength(
      IWorldAccessor world,
      BlockPos heatSourcePos,
      BlockPos heatReceiverPos)
    {
        return Math.Max((float)(((double)this.ovenTemperature - 20.0) / ((double)this.maxTemperature - 20.0) * 8.0), 0.0f);
    }

    protected virtual void OnBurnTick(float dt)
    {
        dt *= 1.0f;  
        if (this.Api is ICoreClientAPI)
            return;


      

        if (GetBehavior<BEBehaviorEOven>()?.powerSetting > 0)
        {

            if (!IsBurning)
            {
                IsBurning = true;

                //Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("state", "enabled")).BlockId, Pos);
                MarkDirty(true);
            }
        }
        else
        {
            if (IsBurning)                     //готовка закончилась
            {
                IsBurning = false;

                //Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("state", "disabled")).BlockId, Pos);
                MarkDirty(true);
                if (!ovenInv.Empty)
                    Api.World.PlaySoundAt(new AssetLocation("electricityaddon:sounds/din_din_din"), Pos.X, Pos.Y, Pos.Z, null, false, 8.0F, 0.4F);
            }
        }



        if (!ovenInv.Empty)   //если не пусто
        {         

            if (this.IsBurning)
            {
                int EnvTemp = this.EnvironmentTemperature();
                //чем больше энергии тем выше будет максимальная достижимая температура
                int power = GetBehavior<BEBehaviorEOven>().powerSetting;
                float toTemp = Math.Max(EnvTemp, power * maxBakingTemperatureAccepted / GetBehavior<BEBehaviorEOven>().maxConsumption);
                this.ovenTemperature = this.ChangeTemperature(this.ovenTemperature, toTemp, dt*1.5F);
                if (this.ovenTemperature > EnvTemp)
                {
                    this.HeatInput(dt);
                }
            }
            else
                this.ovenTemperature = ChangeTemperature(ovenTemperature, EnvironmentTemperature(), dt);
        }            
    
        else                 //выравниваем температуру с окружающей средой
        {
            this.ovenTemperature = ChangeTemperature(ovenTemperature, EnvironmentTemperature(), dt);
        }



        //if (++this.syncCount % 5 != 0 || !this.IsBurning && (double) this.prevOvenTemperature == (double) this.ovenTemperature && this.Inventory[0].Empty && this.Inventory[1].Empty && this.Inventory[2].Empty && this.Inventory[3].Empty)
        // return;
        this.MarkDirty();
        this.prevOvenTemperature = this.ovenTemperature;
    }

    //греем содержимое всей печи
    protected virtual void HeatInput(float dt)
    {
        for (int index = 0; index < this.bakeableCapacity; ++index)
        {
            ItemStack itemstack = this.ovenInv[index].Itemstack;
            if (itemstack != null && (double)this.HeatStack(itemstack, dt, index) >= 100.0)
                this.IncrementallyBake(dt * 1.2f, index);
        }
    }

    //греем конкретно один предмет
    protected virtual float HeatStack(ItemStack stack, float dt, int i)
    {
        float temp = this.bakingData[i].temp;
        float val2_1 = temp;
        if ((double)temp < (double)this.ovenTemperature)
        {
            float dt1 = (1f + GameMath.Clamp((float)(((double)this.ovenTemperature - (double)temp) / 28.0), 0.0f, 1.6f)) * dt;
            val2_1 = this.ChangeTemperature(temp, this.ovenTemperature, dt1);
            CombustibleProperties combustibleProps = stack.Collectible.CombustibleProps;
            int maxTemperature = combustibleProps != null ? combustibleProps.MaxTemperature : 0;
            JsonObject itemAttributes = stack.ItemAttributes;
            int val2_2 = itemAttributes != null ? itemAttributes["maxTemperature"].AsInt() : 0;
            int val1 = Math.Max(maxTemperature, val2_2);
            if (val1 > 0)
                val2_1 = Math.Min((float)val1, val2_1);
        }
        else if ((double)temp > (double)this.ovenTemperature)
        {
            float dt2 = (1f + GameMath.Clamp((float)(((double)temp - (double)this.ovenTemperature) / 28.0), 0.0f, 1.6f)) * dt;
            val2_1 = this.ChangeTemperature(temp, this.ovenTemperature, dt2);
        }
        if ((double)temp != (double)val2_1)
            this.bakingData[i].temp = val2_1;
        return val2_1;
    }

    protected virtual void IncrementallyBake(float dt, int slotIndex)
    {
        ItemSlot itemSlot = this.Inventory[slotIndex];
        OvenItemData ovenItemData = this.bakingData[slotIndex];
        float num1 = ovenItemData.BrowningPoint;
        if ((double)num1 == 0.0)
            num1 = 160f;
        double val = (double)ovenItemData.temp / (double)num1;
        float num2 = ovenItemData.TimeToBake;
        if ((double)num2 == 0.0)
            num2 = 1f;
        float num3 = (float)GameMath.Clamp((int)val, 1, 30) * dt / num2;
        float num4 = ovenItemData.BakedLevel;
        if ((double)ovenItemData.temp > (double)num1)
        {
            num4 = ovenItemData.BakedLevel + num3;
            ovenItemData.BakedLevel = num4;
        }
        BakingProperties bakingProperties = BakingProperties.ReadFrom(itemSlot.Itemstack);
        float num5 = bakingProperties != null ? bakingProperties.LevelFrom : 0.0f;
        float num6 = bakingProperties != null ? bakingProperties.LevelTo : 1f;
        float num7 = (float)(int)((double)GameMath.Mix(bakingProperties != null ? bakingProperties.StartScaleY : 1f, bakingProperties != null ? bakingProperties.EndScaleY : 1f, GameMath.Clamp((float)(((double)num4 - (double)num5) / ((double)num6 - (double)num5)), 0.0f, 1f)) * (double)BlockEntityOven.BakingStageThreshold) / (float)BlockEntityOven.BakingStageThreshold;
        bool flag = (double)num7 != (double)ovenItemData.CurHeightMul;
        ovenItemData.CurHeightMul = num7;
        if ((double)num4 > (double)num6)
        {
            float temp = ovenItemData.temp;
            string resultCode = bakingProperties?.ResultCode;
            if (resultCode != null)                            //степень готовности изменилась
            {
                ItemStack itemStack = (ItemStack)null;
                if (itemSlot.Itemstack.Class == EnumItemClass.Block)
                {
                    Vintagestory.API.Common.Block block = this.Api.World.GetBlock(new AssetLocation(resultCode));
                    if (block != null)
                        itemStack = new ItemStack(block);
                }
                else
                {
                    Vintagestory.API.Common.Item obj = this.Api.World.GetItem(new AssetLocation(resultCode));
                    if (obj != null)
                        itemStack = new ItemStack(obj);
                }
                if (itemStack != null)
                {
                    if (this.ovenInv[slotIndex].Itemstack.Collectible is IBakeableCallback collectible)
                        collectible.OnBaked(this.ovenInv[slotIndex].Itemstack, itemStack);
                    this.ovenInv[slotIndex].Itemstack = itemStack;
                    this.bakingData[slotIndex] = new OvenItemData(itemStack);
                    this.bakingData[slotIndex].temp = temp;
                    flag = true;
                }
            }
            else
            {
                ItemSlot outputSlot = (ItemSlot)new DummySlot((ItemStack)null);
                if (itemSlot.Itemstack.Collectible.CanSmelt(this.Api.World, (ISlotProvider)this.ovenInv, itemSlot.Itemstack, (ItemStack)null))
                {
                    itemSlot.Itemstack.Collectible.DoSmelt(this.Api.World, (ISlotProvider)this.ovenInv, this.ovenInv[slotIndex], outputSlot);
                    if (!outputSlot.Empty)
                    {
                        this.ovenInv[slotIndex].Itemstack = outputSlot.Itemstack;
                        this.bakingData[slotIndex] = new OvenItemData(outputSlot.Itemstack);
                        this.bakingData[slotIndex].temp = temp;
                        flag = true;
                    }
                }
            }
        }
        if (!flag)
            return;
        this.updateMesh(slotIndex);
        this.MarkDirty(true);
    }

    //получает температуру окружающей среды
    protected virtual int EnvironmentTemperature()
    {
        return (int)this.Api.World.BlockAccessor.GetClimateAt(this.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, this.Api.World.Calendar.TotalDays).Temperature;
    }

    //считает прирост температуры
    public virtual float ChangeTemperature(float fromTemp, float toTemp, float dt)
    {
        float num1 = Math.Abs(fromTemp - toTemp);
        float num2 = num1 * GameMath.Sqrt(num1);
        dt += dt * (num2 / 480f);
        if ((double)num2 < (double)dt)
            return toTemp;
        if ((double)fromTemp > (double)toTemp)
            dt = (float)(-(double)dt / 2.0);
        return (double)Math.Abs(fromTemp - toTemp) < 1.0 ? toTemp : fromTemp + dt;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        this.ovenInv.FromTreeAttributes(tree);
        this.burning = tree.GetInt("burn") > 0;
        this.rotationDeg = tree.GetInt("rota");
        this.ovenTemperature = tree.GetFloat("temp");
        for (int i = 0; i < this.bakeableCapacity; ++i)
            this.bakingData[i] = OvenItemData.ReadFromTree(tree, i);
        ICoreAPI api = this.Api;
        if ((api != null ? (api.Side == EnumAppSide.Client ? 1 : 0) : 0) == 0)
            return;
        this.updateMeshes();
        if (this.clientSidePrevBurning == this.IsBurning)
            return;

        this.clientSidePrevBurning = this.IsBurning;
        this.MarkDirty(true);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        this.ovenInv.ToTreeAttributes(tree);
        tree.SetInt("burn", this.burning ? 1 : 0);
        tree.SetInt("rota", this.rotationDeg);
        tree.SetFloat("temp", this.ovenTemperature);
        for (int i = 0; i < this.bakeableCapacity; ++i)
            this.bakingData[i].WriteToTree(tree, i);
    }


    //выводит содержимое и температуры
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder)
    {
        base.GetBlockInfo(forPlayer, stringBuilder);
        stringBuilder.AppendLine();
        for (int slotId = 0; slotId < this.bakeableCapacity; ++slotId)
        {
            if (!this.ovenInv[slotId].Empty)
            {
                ItemStack itemstack = this.ovenInv[slotId].Itemstack;
                stringBuilder.Append(itemstack.GetName());
                stringBuilder.AppendLine(" (" + Lang.Get("{0}°C", (object)(int)this.bakingData[slotId].temp) + ")");
            }
        }
    }


    //поставили духовку
    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(byItemStack);
        var electricity = Electricity;
        if (electricity != null)
        {
            electricity.Connection = Facing.DownAll;
        }
    }



    public override int DisplayedItems
    {
        get => this.OvenContentMode == EnumOvenContentMode.Quadrants ? 4 : 1;
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] numArray = new float[this.DisplayedItems][];
        Vec3f[] vec3fArray = new Vec3f[this.DisplayedItems];
        switch (this.OvenContentMode)
        {
            case EnumOvenContentMode.Firewood:
                vec3fArray[0] = new Vec3f();
                break;
            case EnumOvenContentMode.SingleCenter://положение пирога
                vec3fArray[0] = new Vec3f(0.0f, 1f / 16f, 0.0f);
                break;
            case EnumOvenContentMode.Quadrants://положение хлеба
                vec3fArray[0] = new Vec3f(-0.125f, 1f / 16f, -5f / 32f);
                vec3fArray[1] = new Vec3f(-0.125f, 1f / 16f, 5f / 32f);
                vec3fArray[2] = new Vec3f(3f / 16f, 1f / 16f, -5f / 32f);
                vec3fArray[3] = new Vec3f(3f / 16f, 1f / 16f, 5f / 32f);
                break;
        }
        for (int index = 0; index < numArray.Length; ++index)
        {
            Vec3f vec3f = vec3fArray[index];
            float y = this.OvenContentMode == EnumOvenContentMode.Firewood ? 0.9f : this.bakingData[index].CurHeightMul;
            numArray[index] = new Matrixf().Translate(vec3f.X, vec3f.Y, vec3f.Z).Translate(0.5f, 0.0f, 0.5f).RotateYDeg((float)(this.rotationDeg + (this.OvenContentMode == EnumOvenContentMode.Firewood ? 270 : 0))).Scale(0.9f, y, 0.9f).Translate(-0.5f, 0.0f, -0.5f).Values;
        }
        return numArray;
    }

    protected override string getMeshCacheKey(ItemStack stack)
    {
        string str = "";
        for (int slotId = 0; slotId < this.bakingData.Length; ++slotId)
        {
            if (this.Inventory[slotId].Itemstack == stack)
            {
                str = "-" + this.bakingData[slotId].CurHeightMul.ToString();
                break;
            }
        }
        return (this.OvenContentMode == EnumOvenContentMode.Firewood ? stack.StackSize.ToString() + "x" : "") + base.getMeshCacheKey(stack) + str;
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        this.tfMatrices = this.genTransformationMatrices();
        return base.OnTesselation(mesher, tessThreadTesselator);
    }

    protected override MeshData getOrCreateMesh(ItemStack stack, int index)
    {
        if (this.OvenContentMode != EnumOvenContentMode.Firewood)
            return base.getOrCreateMesh(stack, index);
        MeshData modeldata = this.getMesh(stack);
        if (modeldata != null)
            return modeldata;
        this.nowTesselatingShape = Shape.TryGet((ICoreAPI)this.capi, AssetLocation.Create(this.Block.Attributes["ovenFuelShape"].AsString(), this.Block.Code.Domain).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
        this.nowTesselatingObj = stack.Collectible;
        if (this.nowTesselatingShape == null)
        {
            this.capi.Logger.Error("Stacking model shape for collectible " + (string)stack.Collectible.Code + " not found. Block will be invisible!");
            return (MeshData)null;
        }
        this.capi.Tesselator.TesselateShape("ovenFuelShape", this.nowTesselatingShape, out modeldata, (ITexPositionSource)this, quantityElements: new int?(stack.StackSize));
        this.MeshCache[this.getMeshCacheKey(stack)] = modeldata;
        return modeldata;
    }



}
