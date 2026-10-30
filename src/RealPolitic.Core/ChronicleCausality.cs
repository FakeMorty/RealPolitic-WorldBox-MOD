using System;
using System.Collections.Generic;

namespace RealPolitic.Core;

/// <summary>Observed facts, not guessed causes. Only the death postfix may confirm a death.</summary>
public sealed class ChronicleCausality
{
    private readonly Dictionary<int, DeathRecord> _lastResidents = new Dictionary<int, DeathRecord>();
    public int Count => _lastResidents.Count;
    public bool HasEvidence(int kingdomId) => _lastResidents.ContainsKey(kingdomId);

    public bool Confirm(DeathRecord record, bool deathConfirmed, int populationAfter)
    {
        if (record == null || !record.KingdomId.HasValue || !deathConfirmed
            || record.PopulationBeforeDeath != 1 || populationAfter != 0 || record.WasLastResident) return false;
        record.WasLastResident = true;
        record.LastResidentEvidenceActive = true;
        _lastResidents[record.KingdomId.Value] = record;
        return true;
    }

    public void ObservePopulation(int kingdomId, int population)
    {
        if (population > 0 && _lastResidents.TryGetValue(kingdomId, out DeathRecord record))
        {
            record.LastResidentEvidenceActive = false;
            _lastResidents.Remove(kingdomId);
        }
    }

    public DeathRecord Take(int kingdomId, int year)
    {
        if (!_lastResidents.TryGetValue(kingdomId, out DeathRecord record)) return null;
        _lastResidents.Remove(kingdomId);
        record.LastResidentEvidenceActive = false;
        // Beyond two years, a disappearance is no longer safely attributable to this death.
        return year >= record.Year && year - record.Year <= 2 ? record : null;
    }

    public void Restore(DeathRecord record)
    {
        if (record != null && record.WasLastResident && record.LastResidentEvidenceActive && record.KingdomId.HasValue)
            _lastResidents[record.KingdomId.Value] = record;
    }

    public void Clear() { _lastResidents.Clear(); }
}
