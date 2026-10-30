namespace RealPolitic.Core;

public struct CityInput
{
	public int CityId;

	public int KingdomId;

	public string Name;

	public string BiomeId;

	public int Population;

	public bool FoodOk;

	public bool AtWar;

	public int ReligionId;

	public int LanguageId;

	public int CultureId;

	public string ReligionName;

	public string LanguageName;

	public string CultureName;

	public string RaceId;

	public string RaceName;

	/// <summary>
	/// 0.52: доля главного народа города (0..1). 0 — «игра не сказала»,
	/// тогда город считается однородным.
	/// </summary>
	public float RaceShare;

	public bool IsForeign;

	/// <summary>Лояльность города 0..100 (0 — игра не отдала значение).</summary>
	public int Loyalty;
}
