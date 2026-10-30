namespace RealPolitic.Core;

/// <summary>
/// 0.24: формы союзов после упрощения уний.
/// Vassal — дань без поглощения; Merger — соединение в одну державу: вступивший поглощается мгновенно.
/// (Старые Fedеration/FullUnion сворачиваются в Merger; PersonalUnion удалён.)
/// </summary>
public enum UnionType
{
	Vassal = 0,
	Merger = 1,
}
