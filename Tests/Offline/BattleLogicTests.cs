using System;
using FinalDefense.Combat;

// Expectations come from Docs/Battle/Source/03-numbers.md, not the legacy demo.
// Compiles and exercises the actual production Combat source files.
internal static class BattleLogicTests
{
    private static int assertions;
    private static int scenarios;
    private static int failures;

    internal static void Equal(int expected, int actual, string scenario)
    {
        assertions++;
        if (expected != actual)
            throw new Exception($"{scenario}: expected {expected}, got {actual}");
    }

    internal static void Near(double expected, double actual, string scenario, double epsilon = 0.0001)
    {
        assertions++;
        if (Math.Abs(expected - actual) > epsilon || double.IsNaN(actual))
            throw new Exception($"{scenario}: expected {expected}, got {actual}");
    }

    internal static void True(bool condition, string scenario)
    {
        assertions++;
        if (!condition) throw new Exception(scenario);
    }

    internal static void Run(string name, Action test)
    {
        scenarios++;
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception error)
        {
            failures++;
            Console.Error.WriteLine("FAIL " + name + ": " + error.Message);
        }
    }

    private static void MathTests()
    {
        Run("numeric spec worked example: 1497", () =>
        {
            var hit = DamagePacket.Basic(100);
            hit.skillMultiplier = 3.3;
            hit.flat = 325;
            hit.attackPercent = .2 + .3;
            hit.damageBonus = .3;
            hit.critical = true;
            var target = DefensePacket.Basic(200);
            target.vulnerability = .2;
            target.damageTaken = .3;
            target.reduction = .1;
            target.shelter = .1;
            target.skillMultiplier = 3.3;
            target.flat = -100;
            target.defensePercent = .1;
            Equal(1497, CombatMath.Damage(hit, target), "Complete six-zone damage settlement");
        });

        Run("same-type buffs add and distinct zones multiply", () =>
        {
            var hit = DamagePacket.Basic(100);
            hit.attackPercent = .2 + .3 + .4;
            Equal(190, CombatMath.Damage(hit, DefensePacket.Basic(0)), "Three attack buffs add to 90%");
            hit.attackPercent = .2;
            hit.damageBonus = .3;
            var target = DefensePacket.Basic(0);
            target.vulnerability = .4;
            Equal(218, CombatMath.Damage(hit, target), "Distinct zones multiply to 218.4 before settlement");
        });

        Run("five-percent fallback applies only to negative damage", () =>
        {
            Equal(80, CombatMath.Damage(DamagePacket.Basic(100), DefensePacket.Basic(20)), "Defense subtracts from attack");
            Equal(0, CombatMath.Damage(DamagePacket.Basic(100), DefensePacket.Basic(100)), "Exactly zero stays zero");
            Equal(1, CombatMath.Damage(DamagePacket.Basic(100), DefensePacket.Basic(99)), "Positive damage below 5% stays unchanged");
            Equal(5, CombatMath.Damage(DamagePacket.Basic(100), DefensePacket.Basic(101)), "Negative damage receives 5% initial attack");
            Equal(0, CombatMath.Damage(DamagePacket.Basic(1), DefensePacket.Basic(20)), "No legacy minimum-one clamp");
            var hit = DamagePacket.Basic(100);
            hit.skillMultiplier = 3;
            hit.flat = 100;
            Equal(5, CombatMath.Damage(hit, DefensePacket.Basic(1000)), "Fallback uses initial attack, not skill-scaled attack");
        });

        Run("round to two decimals then round integer away from zero", () =>
        {
            Equal(0, CombatMath.Damage(DamagePacket.Basic(.494), DefensePacket.Basic(0)), "0.494 becomes 0.49 then 0");
            Equal(1, CombatMath.Damage(DamagePacket.Basic(.495), DefensePacket.Basic(0)), "0.495 becomes 0.50 then 1");
            Equal(3, CombatMath.Damage(DamagePacket.Basic(2.5), DefensePacket.Basic(0)), "2.5 rounds away from zero rather than to even");
            var hit = DamagePacket.Basic(10);
            hit.critical = true;
            Equal(15, CombatMath.Damage(hit, DefensePacket.Basic(0)), "Critical damage is +50%");
            Equal(0, CombatMath.Damage(hit, DefensePacket.Basic(10)), "Critical multiplier does not turn zero into fallback damage");
        });

        Run("attack speed follows 30-fps frame examples", () =>
        {
            Equal(30, CombatMath.AttackFrames(30, 0), "Base thirty frames is one second");
            Equal(15, CombatMath.AttackFrames(30, .5f), "Plus 50% turns thirty frames into fifteen");
            Equal(58, CombatMath.AttackFrames(45, -.3f), "Minus 30% floors 58.5 to fifty-eight frames");
            Equal(1, CombatMath.AttackFrames(30, 5), "Extreme speed retains an executable one-frame interval");
        });

        Run("attack speed avoids floating-point one-frame loss", () =>
        {
            Equal(12, CombatMath.AttackFrames(30, .6f), "Thirty frames at +60% is exactly twelve");
            Equal(6, CombatMath.AttackFrames(30, .8f), "Thirty frames at +80% is exactly six");
        });

        Run("directional range rotates with unit facing", () =>
        {
            True(CombatMath.InRange(0, 0, 1, 0, 2, 0, 1, 2), "Forward target is in range");
            True(!CombatMath.InRange(0, 0, 1, 0, -1, 0, 1, 2), "Rear target is out of range");
            True(!CombatMath.InRange(0, 0, 1, 0, 1, 2, 1, 2), "Lateral target outside one-cell width is rejected");
            True(CombatMath.InRange(0, 0, 0, 1, 0, 2, 1, 2), "North-facing range rotates north");
            True(!CombatMath.InRange(0, 0, 0, 1, 2, 0, 1, 2), "Rotation also rotates rejected cells");
        });

        Run("level arrays select exact authored values", () =>
        {
            var unit = new UnitDefinition();
            var values = new[] { 1300, 1550, 1820 };
            Equal(1300, unit.AtLevel(values, 1), "Level one value");
            Equal(1550, unit.AtLevel(values, 2), "Level two value");
            Equal(1820, unit.AtLevel(values, 3), "Level three value");
            Equal(1820, unit.AtLevel(values, 4), "Out-of-range high level safely clamps");
            Equal(0, unit.AtLevel(null, 1), "Missing optional array safely resolves to zero");
        });

        Run("even-width front ranges cover exact cell counts under every rotation", () =>
        {
            foreach (var facing in new[] { new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 0 }, new[] { 0, -1 } })
            {
                int count = 0;
                for (int x = -4; x <= 4; x++) for (int y = -4; y <= 4; y++)
                {
                    bool actual = CombatMath.InRange(0, 0, facing[0], facing[1], x, y, 2, 3);
                    int front = x * facing[0] + y * facing[1];
                    int side = -x * facing[1] + y * facing[0];
                    bool expected = front >= 1 && front <= 3 && side >= 0 && side <= 1;
                    True(actual == expected, "2x3 range uses front rows 1..3 and local side columns 0..1");
                    if (actual) count++;
                }
                Equal(6, count, "Rotated 2x3 front range has exactly six grid centers");
                True(!CombatMath.InRange(0, 0, facing[0], facing[1], 0, 0, 2, 3), "Front range excludes caster row");
            }
        });
    }

    public static int Main()
    {
        MathTests();
        CombatDataTests.Run();
        CombatFactoryTests.RunAll();
        CombatSimulationTests.Run();
        CombatSkillTests.RunAll();
        CombatItemTests.RunAll();
        Level1Playthrough.Run();
        CampaignPlaythrough.Run();
        Console.WriteLine($"{scenarios - failures}/{scenarios} scenarios passed; {assertions} assertions; {failures} failed.");
        Console.WriteLine("Scope: current Combat production rules; legacy Demo DamageCalculator is excluded.");
        return failures == 0 ? 0 : 1;
    }
}
