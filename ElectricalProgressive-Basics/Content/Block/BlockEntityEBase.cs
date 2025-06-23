using ElectricalProgressive.Utils;
using Vintagestory.API.Common;

namespace ElectricalProgressive.Content.Block;

public abstract class BlockEntityEBase : BlockEntity
{
    protected BEBehaviorElectricalProgressive? ElectricalProgressive => GetBehavior<BEBehaviorElectricalProgressive>();

    /// <summary>
    /// Передает значения из Block в BEBehaviorElectricalProgressive
    /// </summary>
    public (EParams, int) Eparams
    {
        get => this.ElectricalProgressive?.Eparams ?? (new(), 0);
        set => this.ElectricalProgressive!.Eparams = value;
    }

    /// <summary>
    /// Передает значения из Block в BEBehaviorElectricalProgressive
    /// </summary>
    public EParams[]? AllEparams
    {
        get => this.ElectricalProgressive?.AllEparams ?? new EParams[]
        {
            new(),
            new(),
            new(),
            new(),
            new(),
            new()
        };
        set
        {
            if (this.ElectricalProgressive != null)
                this.ElectricalProgressive.AllEparams = value;
        }
    }

    public const string AllEparamsKey = "electricalprogressive:allEparams";
}