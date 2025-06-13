using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ElectricalProgressive.Utils;

public static class UIHelper
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="elementBounds"></param>
    /// <param name="isImmersiveMouseMode"></param>
    /// <returns></returns>
    public static ElementBounds AddImmersiveOffset(this ElementBounds elementBounds, bool isImmersiveMouseMode = false)
    {
        var alignment = isImmersiveMouseMode
            ? EnumDialogArea.RightMiddle
            : EnumDialogArea.CenterMiddle;

        var (xOffset, yOffset) = isImmersiveMouseMode
            ? (-12, 0)
            : (20, 0);

        return elementBounds
            .WithAlignment(alignment)
            .WithFixedAlignmentOffset(xOffset, yOffset);
    }
}