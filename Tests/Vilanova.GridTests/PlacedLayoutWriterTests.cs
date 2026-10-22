using System.Collections.Generic;
using Vilanova;
using Xunit;

namespace Vilanova.GridTests;

public class PlacedLayoutWriterTests
{
	[Fact]
	public void KeepsOnlyPiecesPlacedThisSession()
	{
		var text = PlacedLayoutWriter.Format(new List<PlacedLayoutWriter.Piece>
		{
			new() { InstanceId = "plaza:well", DefinitionId = "stone_well", CellX = 1, CellZ = 2, RotationSteps = 0 },
			new() { InstanceId = "placed:b", DefinitionId = "oak_tree", CellX = 10, CellZ = 4, RotationSteps = 1 },
			new() { InstanceId = "scene:Props/Oak", DefinitionId = "oak_tree", CellX = 3, CellZ = 3, RotationSteps = 0 },
			new() { InstanceId = "placed:a", DefinitionId = "cottage_lantern", CellX = 8, CellZ = 9, RotationSteps = 5 },
		});

		Assert.Contains("cottage_lantern 8 9 1 90", text);
		Assert.Contains("oak_tree 10 4 1 90", text);
		Assert.DoesNotContain("stone_well", text);
		Assert.DoesNotContain("scene:", text);
		Assert.True(text.IndexOf("cottage_lantern") < text.IndexOf("oak_tree"));
	}
}
