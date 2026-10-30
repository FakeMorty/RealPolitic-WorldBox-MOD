using System.Collections.Generic;

namespace RealPolitic.Core;

public sealed class DiplomaticMove
{
	public readonly List<string> Reasons = new List<string>();

	public DiplomaticAction Action { get; set; }

	public int TargetId { get; set; }

	public float Score { get; set; }

	public override string ToString()
	{
		return Action.ToString() + " → #" + TargetId + " (" + Score.ToString("F1") + ")";
	}
}
