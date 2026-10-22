using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vilanova;

/// <summary>
/// Text list of pieces placed during this session, ready to copy into the opening layout.
/// </summary>
public static class PlacedLayoutWriter
{
	public const string RelativePath = "Layout/placed_layout.txt";

	public readonly struct Piece
	{
		public string InstanceId { get; init; }
		public string DefinitionId { get; init; }
		public int CellX { get; init; }
		public int CellZ { get; init; }
		public int RotationSteps { get; init; }
	}

	public static string Format(IEnumerable<Piece> pieces)
	{
		var lines = new List<string>();
		if (pieces != null)
		{
			foreach (var piece in pieces)
			{
				if (piece.InstanceId == null || !piece.InstanceId.StartsWith("placed:", StringComparison.Ordinal))
				{
					continue;
				}

				if (string.IsNullOrEmpty(piece.DefinitionId))
				{
					continue;
				}

				var steps = FootprintHelper.NormalizeRotationSteps(piece.RotationSteps);
				var yaw = FootprintHelper.RotationDegrees(steps).ToString("0", CultureInfo.InvariantCulture);
				lines.Add(FormattableString.Invariant($"{piece.DefinitionId} {piece.CellX} {piece.CellZ} {steps} {yaw}"));
			}
		}

		lines.Sort(StringComparer.Ordinal);
		var text = new StringBuilder();
		text.AppendLine("# Peças colocadas nesta sessão. id célulaX célulaZ passos yaw");
		foreach (var line in lines)
		{
			text.AppendLine(line);
		}

		return text.ToString();
	}
}
