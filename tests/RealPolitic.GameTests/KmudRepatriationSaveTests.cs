using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

/// <summary>
/// 0.47: сохранение и загрузка помнят пятый вопрос КМУД.
///
/// Это важнее, чем кажется: если новые поля не попали в сейв, то после загрузки
/// игра забудет, о каком народе голосовали и куда его возвращают, — и решение
/// «вернуть народ» перестанет исполняться. Поэтому проверяем круговорот
/// «записали → прочитали» на настоящем сохранении мода.
/// </summary>
public class KmudRepatriationSaveTests
{
	[Fact]
	public void RepatriationQuestionSurvivesSaveAndLoad()
	{
		PoliticalWorld world = new PoliticalWorld();
		world.Kmud.Phase = KmudPhase.Closed;
		world.Kmud.OpenedYear = 300;
		world.Kmud.ClosesYear = 310;
		world.Kmud.Members = 4;
		world.Kmud.Agenda.RepatriationRaceId = "fox";
		world.Kmud.Agenda.RepatriationRaceName = "лисы";
		world.Kmud.Agenda.RepatriationToId = 7;
		world.Kmud.Agenda.RepatriationFromId = 3;
		world.Kmud.Decisions.Repatriation = KmudOptions.RepatGuard;
		world.Kmud.Decisions.RepatriationRaceId = "fox";
		world.Kmud.Decisions.RepatriationToId = 7;
		world.Kmud.Decisions.RepatriatedCount = 25;

		string json = SaveData.Write(world);

		PoliticalWorld loaded = new PoliticalWorld();
		SaveData.Apply(loaded, json);

		Assert.True(loaded.Kmud.Agenda.HasRepatriation);
		Assert.Equal("fox", loaded.Kmud.Agenda.RepatriationRaceId);
		Assert.Equal("лисы", loaded.Kmud.Agenda.RepatriationRaceName);
		Assert.Equal(7, loaded.Kmud.Agenda.RepatriationToId);
		Assert.Equal(3, loaded.Kmud.Agenda.RepatriationFromId);
		Assert.Equal(KmudOptions.RepatGuard, loaded.Kmud.Decisions.Repatriation);
		Assert.Equal("fox", loaded.Kmud.Decisions.RepatriationRaceId);
		Assert.Equal(7, loaded.Kmud.Decisions.RepatriationToId);
		Assert.Equal(25, loaded.Kmud.Decisions.RepatriatedCount);
	}

	[Fact]
	public void OldSaveWithoutRepatriationFieldsLoadsWithoutErrors()
	{
		// Сохранение из версии до 0.47: полей пятого вопроса в нём нет вообще.
		PoliticalWorld world = new PoliticalWorld();
		world.Kmud.Phase = KmudPhase.Closed;
		world.Kmud.OpenedYear = 200;
		world.Kmud.ClosesYear = 210;
		world.Kmud.Members = 2;
		string json = SaveData.Write(world);

		PoliticalWorld loaded = new PoliticalWorld();
		SaveData.Apply(loaded, json);

		Assert.False(loaded.Kmud.Agenda.HasRepatriation);
		Assert.Equal(0, loaded.Kmud.Decisions.Repatriation);
		Assert.Equal(string.Empty, loaded.Kmud.Decisions.RepatriationRaceId);
		Assert.Equal(0, loaded.Kmud.Decisions.RepatriatedCount);
	}
}
