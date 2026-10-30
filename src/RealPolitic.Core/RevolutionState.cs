namespace RealPolitic.Core;

public sealed class RevolutionState
{
	public IdeologyType Target { get; set; }

	public ReformStance TargetStance { get; set; }

	public int StartedYear { get; set; }

	public int YearsLeft { get; set; }
}
