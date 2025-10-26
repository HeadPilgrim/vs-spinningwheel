using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace SpinningWheel.Utilities;
public class ValuesByMultiblockOffset
{
    public Dictionary<Vec3i, Cuboidf[]> CollisionBoxesByOffset { get; set; } = [];
    public Dictionary<Vec3i, Cuboidf[]> SelectionBoxesByOffset { get; set; } = [];

    public static ValuesByMultiblockOffset FromAttributes(Block block)
    {
        ValuesByMultiblockOffset properties = new ValuesByMultiblockOffset();
        Dictionary<string, RotatableCube[]> rawCollisionsAndSelections = block.Attributes?["valuesByMultiblockOffset"]?["collisionSelectionBoxes"]?.AsObject<Dictionary<string, RotatableCube[]>>();

        if (rawCollisionsAndSelections != null && rawCollisionsAndSelections.Count > 0)
        {
            properties.CollisionBoxesByOffset = ToCuboidsByOffsets(rawCollisionsAndSelections);
            properties.SelectionBoxesByOffset = ToCuboidsByOffsets(rawCollisionsAndSelections);
        }
        else
        {
            properties.CollisionBoxesByOffset = ToCuboidsByOffsets(block.Attributes?["valuesByMultiblockOffset"]?["collisionBoxes"]?.AsObject<Dictionary<string, RotatableCube[]>>());
            properties.SelectionBoxesByOffset = ToCuboidsByOffsets(block.Attributes?["valuesByMultiblockOffset"]?["selectionBoxes"]?.AsObject<Dictionary<string, RotatableCube[]>>());
        }
        return properties;
    }

    private static Dictionary<Vec3i, Cuboidf[]> ToCuboidsByOffsets(Dictionary<string, RotatableCube[]> rawCuboidsByOffsets)
    {
        Dictionary<Vec3i, Cuboidf[]> cuboidsByOffsets = [];

        if (rawCuboidsByOffsets == null) return cuboidsByOffsets;

        foreach ((string stringOffset, RotatableCube[] colBoxes) in rawCuboidsByOffsets)
        {
            if (string.IsNullOrEmpty(stringOffset)) continue;

            string[] offsetArray = stringOffset.Split(',', 3);

            if (offsetArray == null || offsetArray.Length != 3) continue;

            int[] offsetNumArray = offsetArray.Select(x => StringUtil.ToInt(x)).ToArray();

            cuboidsByOffsets.Add(new Vec3i(offsetNumArray[0], offsetNumArray[1], offsetNumArray[2]), ToCuboidf(colBoxes));
        }

        return cuboidsByOffsets;
    }

    public static Cuboidf[] ToCuboidf(params RotatableCube[] cubes)
    {
        Cuboidf[] outcubes = new Cuboidf[cubes.Length];
        for (int i = 0; i < cubes.Length; i++)
        {
            outcubes[i] = cubes[i].RotatedCopy();
        }
        return outcubes;
    }
}