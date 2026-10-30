using System.Collections.Generic;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.Tests;

public class DiplomacyAdvisorTests
{
	private static DiplomatSnapshot Snap(int id, IdeologyType ideology, int warriors, int population, bool atWar = false, bool starving = false, float legitimacy = 80f)
	{
		return new DiplomatSnapshot
		{
			KingdomId = id,
			Ideology = ideology,
			Stance = ReformStance.Moderate,
			Legitimacy = legitimacy,
			Population = population,
			Warriors = warriors,
			Starving = starving,
			AtWar = atWar
		};
	}

	[Fact]
	public void MilitaristMuchStrongerThanRivalNeighbour_DeclaresWar()
	{
		DiplomatSnapshot self = Snap(1, IdeologyType.Militarism, warriors: 100, population: 1000);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Egalitarianism, warriors: 5, population: 50)
		};
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = true }
		};

		DiplomaticMove move = DiplomaticAdvisor.BestMove(self, others, relations);

		Assert.NotNull(move);
		Assert.Equal(DiplomaticAction.DeclareWar, move.Action);
		Assert.Equal(2, move.TargetId);
	}

	[Fact]
	public void KingdomLosingWar_SeeksPeace()
	{
		DiplomatSnapshot self = Snap(1, IdeologyType.Monarchy, warriors: 5, population: 50, atWar: true, starving: true, legitimacy: 30f);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Militarism, warriors: 100, population: 1000)
		};
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.War, Adjacent = true }
		};

		DiplomaticMove move = DiplomaticAdvisor.BestMove(self, others, relations);

		Assert.NotNull(move);
		Assert.Equal(DiplomaticAction.MakePeace, move.Action);
		Assert.Equal(2, move.TargetId);
	}

	[Fact]
	public void PacifistWeakKingdom_DoesNotDeclareWarOnStrongerNeighbour()
	{
		// Либерализм (в 0.22 — «изоляционизм») не лезет в войну с вдесятеро сильнейшим соседом:
		// единственный наступательный ход (война) слабее порога ответа —
		// советник уходит «в ничью» (Hold → BestMove возвращает null).
		DiplomatSnapshot self = Snap(1, IdeologyType.Liberalism, warriors: 5, population: 50);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Militarism, warriors: 100, population: 1000)
		};
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = true }
		};

		DiplomaticMove move = DiplomaticAdvisor.BestMove(self, others, relations);

		Assert.Null(move);
	}

	[Fact]
	public void SharedIdeologyWeakNeighbour_ProposesAllianceOverWar()
	{
		// Оба — монархии (сродство 1.0), соседи, сопоставимая сила: союз выгоднее войны.
		DiplomatSnapshot self = Snap(1, IdeologyType.Monarchy, warriors: 20, population: 200);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Monarchy, warriors: 20, population: 200)
		};
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = true }
		};

		DiplomaticMove move = DiplomaticAdvisor.BestMove(self, others, relations);

		Assert.NotNull(move);
		Assert.Equal(DiplomaticAction.ProposeAlliance, move.Action);
	}

	[Fact]
	public void FarAwayWeakKingdom_NoWarWithoutCommonBorder_EvenIfMuchStronger()
	{
		// Далёкая держава без общей границы — ВОЙНА невозможна, как бы она ни была слаба:
		// советник может предложить что угодно (вассалитет, союз), но не объявление войны.
		DiplomatSnapshot self = Snap(1, IdeologyType.Militarism, warriors: 500, population: 5000);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Egalitarianism, warriors: 5, population: 50),
			Snap(3, IdeologyType.Monarchy, warriors: 20, population: 200)
		};
		// Оба не соседи (Adjacent = false): объявление войны исключено против кого бы то ни было.
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = false },
			new RelationInfo { OtherId = 3, Relation = RelationType.Peace, Adjacent = false }
		};

		DiplomaticMove move = DiplomaticAdvisor.BestMove(self, others, relations);

		Assert.True(move == null || move.Action != DiplomaticAction.DeclareWar,
			"Война без общей границы невозможна (получено: " + (move?.Action.ToString() ?? "null") + ")");
	}

	[Fact]
	public void FarAwayWeakKingdom_NoDeclareWarMove_ButCanAlly_WithoutCommonBorder()
	{
		// С далёкой державой союз/прочие ходы допустимы (через послов), но не война.
		DiplomatSnapshot self = Snap(1, IdeologyType.Militarism, warriors: 500, population: 5000);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Egalitarianism, warriors: 5, population: 50)
		};
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.Peace, Adjacent = false }
		};

		List<DiplomaticMove> moves = DiplomaticAdvisor.Evaluate(self, others, relations);

		Assert.DoesNotContain(moves, m => m.TargetId == 2 && m.Action == DiplomaticAction.DeclareWar);
		Assert.Contains(moves, m => m.Action == DiplomaticAction.ProposeAlliance && m.TargetId == 2);
	}

	[Fact]
	public void AllyIsNeverOfferedAsADeclareWarCandidate()
	{
		DiplomatSnapshot self = Snap(1, IdeologyType.Militarism, warriors: 500, population: 5000);
		List<DiplomatSnapshot> others = new List<DiplomatSnapshot>
		{
			Snap(2, IdeologyType.Egalitarianism, warriors: 5, population: 50)
		};
		List<RelationInfo> relations = new List<RelationInfo>
		{
			new RelationInfo { OtherId = 2, Relation = RelationType.Alliance, Adjacent = true }
		};

		List<DiplomaticMove> moves = DiplomaticAdvisor.Evaluate(self, others, relations);

		Assert.DoesNotContain(moves, m => m.TargetId == 2 && m.Action == DiplomaticAction.DeclareWar);
	}
}
