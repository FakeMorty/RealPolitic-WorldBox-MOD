using System;
using RealPolitic.Core;
using NeoModLoader.services;

namespace RealPolitic;

/// <summary>
/// Адаптер между чистым стратегическим ядром и API WorldBox.
/// Дипломатические планы и локальные стратегические действия выполняются через
/// безопасные игровые адаптеры; несовместимые вызовы возвращают отказ без падения игры.
/// </summary>
public sealed class GameKingdomActionExecutor : IKingdomActionExecutor
{
    private readonly WorldFeeder _feeder;
    private readonly KingdomInput _input;

    public GameKingdomActionExecutor(WorldFeeder feeder, KingdomInput input)
    {
        _feeder = feeder;
        _input = input;
    }

	public bool TryExecute(KingdomActionPlan plan, KingdomStrategySnapshot snapshot, out string result)
	{
		try
		{
			if (plan == null)
			{
				result = GameText.Localized("RP_RES_PLAN_UNAVAILABLE", "стратегический план недоступен");
				return false;
			}
			if (snapshot == null)
			{
				result = GameText.Localized("RP_RES_SNAPSHOT_UNAVAILABLE", "снимок государства недоступен");
				return false;
			}
			if (snapshot.KingdomId != _input.KingdomId)
			{
				result = GameText.Localized("RP_RES_SNAPSHOT_OTHER_STATE", "снимок относится к другому государству");
				return false;
			}
			if (plan.IsFallback)
			{
				result = GameText.Localized("RP_RES_NO_ACTION", "действие отсутствует — безопасный пропуск");
				return false;
			}
			if (plan.Action == KingdomActionType.SeekAlliance)
            {
                int target = FindTarget(RelationType.Peace, RelationType.None);
                if (target < 0) { result = GameText.Localized("RP_RES_NO_PEACEFUL_NEIGHBOR", "мирный сосед для союза не найден"); return false; }
                bool done = _feeder.TryExecuteStrategicDiplomacy(_input.KingdomId, target, DiplomaticAction.ProposeAlliance, plan.Score);
                result = done ? "союз применён" : "игровой API союза отклонил действие";
                return done;
            }
            if (plan.Action == KingdomActionType.SeekPeace)
            {
                int target = FindTarget(RelationType.War);
                if (target < 0) { result = GameText.Localized("RP_RES_NO_WARRING_NEIGHBOR", "воюющий сосед не найден"); return false; }
                bool done = _feeder.TryExecuteStrategicDiplomacy(_input.KingdomId, target, DiplomaticAction.MakePeace, plan.Score);
                result = done ? "мир применён" : "игровой API мира отклонил действие";
                return done;
            }
            bool applied = _feeder.TryExecuteStrategicState(_input.KingdomId, plan, snapshot, out result);
            return applied;
        }
        catch (Exception ex)
        {
            LogService.LogError("[Real Politic] безопасный стратегический адаптер: " + ex.Message);
            result = GameText.Localized("RP_RES_ADAPTER_EXCEPTION", "исключение адаптера перехвачено");
            return false;
        }
    }

    private int FindTarget(RelationType first, RelationType second = RelationType.None)
    {
        if (_input.Relations == null) return -1;
        for (int i = 0; i < _input.Relations.Count; i++)
        {
            RelationInfo relation = _input.Relations[i];
            if (relation.Relation == first || relation.Relation == second) return relation.OtherId;
        }
        return -1;
    }
}
