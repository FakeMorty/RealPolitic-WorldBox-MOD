namespace RealPolitic.Core;

public enum IdeologyType
{
	Tribalism,
	Monarchy,
	Theocracy,
	Aristocracy,
	MerchantRepublic,
	Militarism,
	Egalitarianism,
	Technocracy,
	Liberalism,
	Democracy,
	Communism,
	Ethnonationalism,
	Khaganate,
	Empire,
	FreeTerritory,

	/// <summary>
	/// 0.49: Национализм — строй обиженной родины (заказ владельца 29.10.2026).
	/// Его берут жители державы, чья родная страна была поглощена: федерация и уния —
	/// самые горячие, вассалитет — средне, прямой захват — тише. В отличие от
	/// «Этнонационализма», который притесняет иные народы, гражданский национализм
	/// воюет не с расами, а за возвращение своего государства. Придя к власти, он
	/// НЕ правит сам: он возвращает прежний строй державы (см. NationalismEngine).
	/// </summary>
	Nationalism
}
