namespace RealPolitic.Core;

public enum DeathCause
{
	Unknown,
	OldAge,
	KilledInBattle,
	KilledByUnit,
	Assassination,
	Executed,
	DiedInRebellion,
	Starvation,
	Disease,
	Fire,
	Disaster,
	AnimalAttack,
	DivineWrath,
	Sacrifice,
	Curse,
	Drowning,
	Frozen,
	Overwork,
	Explosion,
	Fell,
	/// <summary>Гибель от пушки корабля (boat_trading/transport/… в игре — это суда, а не звери). 0.23.12.</summary>
	KilledByBoat
}
