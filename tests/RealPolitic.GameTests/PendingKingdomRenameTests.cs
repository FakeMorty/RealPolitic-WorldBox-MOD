using System.Collections.Generic;
using System.Reflection;
using RealPolitic.Core;
using Xunit;

namespace RealPolitic.GameTests;

public sealed class PendingKingdomRenameTests
{
    private sealed class RenameKingdom : Kingdom
    {
        public RenameKingdom()
        {
            cities = new List<City>();
            units = new List<Actor>();
            data = new KingdomData();
        }

        // В настоящей игре NanoObject.name отражает data.name; стаб хранит их отдельно.
        public override string name
        {
            get => data == null ? string.Empty : data.name;
            set
            {
                if (data != null)
                {
                    data.name = value;
                }
            }
        }
    }

    [Fact]
    public void PendingRename_IsWrittenToLiveKingdom_AndClearedOnlyAfterSuccess()
    {
        var world = new PoliticalWorld();
        world.AddKingdom(7, IdeologyType.Empire, 1);
        world.SetPendingRenameOverride(7, "Добрвецкая Империя");

        var kingdom = new RenameKingdom();
        kingdom.data.id = 7;
        kingdom.data.name = "Добрвецане";

        var feeder = new WorldFeeder(world);
        FieldInfo liveField = typeof(WorldFeeder).GetField("_liveKingdoms", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var live = (Dictionary<int, Kingdom>)liveField.GetValue(feeder)!;
        live[7] = kingdom;

        MethodInfo apply = typeof(WorldFeeder).GetMethod("ApplyPendingKingdomRenames", BindingFlags.Instance | BindingFlags.NonPublic)!;
        apply.Invoke(feeder, null);

        Assert.Equal("Добрвецкая Империя", kingdom.data.name);
        Assert.Equal("Добрвецкая Империя", ((NanoObject)kingdom).name);
        Assert.False(world.TryGetPendingRenameOverride(7, out _));
    }
}
