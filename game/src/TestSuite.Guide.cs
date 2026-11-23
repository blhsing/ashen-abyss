using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// --suite=guide (SYSTEMS): the 武器指南. Content: every class lists exactly its loadout bases, every base has its notes,
/// comparisons and stats; focus by Focus / Item / ClassId. Entry points: the class introduction (main menu), G on a
/// hovered weapon, G with nothing hovered, the sheet's 武器指南 button. Run with --slot=&lt;your test slot&gt;.
/// </summary>
public sealed partial class TestSuite
{
    private WeaponGuideUi GuidePanel => G.Ui?.Find("guide") as WeaponGuideUi;

    private static Button FindButton(Node root, string text)
    {
        if (root == null) return null;
        if (root is Button b && b.Text == text && b.IsVisibleInTree()) return b;
        foreach (var c in root.GetChildren()) { var f = FindButton(c, text); if (f != null) return f; }
        return null;
    }

    private static void PressKey(Key k)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
    }

    private IEnumerator GuideSuite()
    {
        _section = "guide: content";
        var ui = G.Ui;
        if (!Check(ui != null && GuidePanel != null, "guide: the panel is registered")) yield break;
        foreach (ClassId c in Enum.GetValues<ClassId>())
        {
            ui.Open("guide", c);
            yield return null;
            var p = GuidePanel;
            var g = ItemDb.Loadout(c);
            var want = g.TwoHand.Concat(g.MainHand).Concat(g.OffHand).Distinct().OrderBy(x => x).ToList();
            var shown = p.ShownEntries.Where(x => x != null).OrderBy(x => x).ToList();
            bool notes = shown.All(b => WeaponGuide.Notes.ContainsKey(b));
            bool cmp = shown.All(b => WeaponGuide.Alternatives(c, b).All(a => WeaponGuide.Compare(b, a).Length > 4));
            Check(p.ShownClass == c && p.ShownBase == null && shown.SequenceEqual(want) && notes && cmp,
                $"guide {Names.ClassKey(c)}: lists exactly its {want.Count} bases, each with notes and comparisons", string.Join(" ", p.ShownEntries.Select(x => x ?? "rules")));
            ui.Close("guide");
        }
        Check(ItemDb.GearBases.All(b => WeaponGuide.Notes.ContainsKey(b.Id)), "guide: every base in the game has an entry");
        // focus: an explicit base, an item of the hero's class, an item no hero class holds (another class is chosen)
        ui.Open("guide", new WeaponGuideUi.Focus { Class = ClassId.Lampknight, Base = "lantern" });
        yield return null;
        Check(GuidePanel.ShownClass == ClassId.Lampknight && GuidePanel.ShownBase == "lantern", "guide: opens focused on the asked base");
        ui.Close("guide");
        var wand = ItemDb.Generate(200, Rarity.Rare, ClassId.Astromancer, ItemKind.Weapon, new Random(5), "wand");
        ui.Open("guide", wand);
        yield return null;
        Check(GuidePanel.ShownBase == "wand" && WeaponGuide.BasesOf(GuidePanel.ShownClass).Contains("wand"), "guide: an item opens its base (a class that holds it)", Names.ClassKey(GuidePanel.ShownClass));
        ui.Close("guide");
        yield return null;

        // entry point: the class introduction at hero creation
        _section = "guide: class intro";
        if (G.State == Game.GameState.Menu)
        {
            MainMenu menu = null;
            if (G.UiLayer != null) foreach (var n in G.UiLayer.GetChildren()) if (n is MainMenu m) menu = m;
            menu?.Open("class:forgebody");
            yield return 1.0f;
            var btn = FindButton(G.UiLayer, "武器指南");
            btn?.EmitSignal(BaseButton.SignalName.Pressed);
            yield return 0.4f;
            Check(btn != null && ui.IsOpen("guide") && GuidePanel.ShownClass == ClassId.Forgebody, "guide: the class introduction opens it on the chosen class", btn == null ? "no button" : Names.ClassKey(GuidePanel.ShownClass));
            if (_arg.Contains("shots")) { G.HarnessShot("Y:/shots/guide/guide_from_classintro.png"); yield return 0.5f; }
            ui.Close("guide");
            yield return 0.2f;
        }
        else Log.Warn("suite", "guide: not at the menu, class intro entry not tested");

        // in play: G on a hovered item, G with nothing hovered, the sheet button
        _section = "guide: in play";
        yield return StartHero(ClassId.Lampknight, 20, "town", gear: true);
        var h = G.Hero;
        ui.Open("inventory");
        yield return 0.5f;
        var pole = ItemDb.Generate(200, Rarity.Rare, ClassId.Lampknight, ItemKind.Weapon, new Random(7), "polearm");
        h.AddItem(pole);
        SlotBoard.HoveredItem = pole;                 // as if the pointer rested on it
        PressKey(Key.G);
        yield return 0.3f;
        var worn = WeaponGuide.EquippedAlternative(h, "polearm");
        Check(ui.IsOpen("guide") && GuidePanel.ShownBase == "polearm" && GuidePanel.ShownClass == ClassId.Lampknight && worn != null,
            "guide: G on a hovered weapon opens it on that base, with the equipped alternative", $"base={GuidePanel.ShownBase} worn={worn?.Base}");
        if (_arg.Contains("shots")) { G.HarnessShot("Y:/shots/guide/guide_focus_polearm.png"); yield return 0.5f; }
        PressKey(Key.G);
        yield return 0.3f;
        Check(!ui.IsOpen("guide"), "guide: G again closes it");
        SlotBoard.HoveredItem = null;
        PressKey(Key.G);
        yield return 0.3f;
        Check(ui.IsOpen("guide") && GuidePanel.ShownBase == null && GuidePanel.ShownClass == h.Class, "guide: G with nothing hovered opens the class page (rules)");
        if (_arg.Contains("shots")) { G.HarnessShot("Y:/shots/guide/guide_rules.png"); yield return 0.5f; }
        ui.Close("guide");
        yield return 0.2f;
        ui.Open("inventory");
        yield return 0.5f;
        var sheetBtn = FindButton(G.UiLayer, "武器指南（G）");
        sheetBtn?.EmitSignal(BaseButton.SignalName.Pressed);
        yield return 0.3f;
        Check(sheetBtn != null && ui.IsOpen("guide") && GuidePanel.ShownClass == h.Class, "guide: the sheet's 可用武器 button opens it");
        ui.CloseAll();
        h.RemoveItem(pole);
        yield return null;
    }
}
