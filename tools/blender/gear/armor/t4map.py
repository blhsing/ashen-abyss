"""t4map -- which part of today's hero goes into which t4 armour piece.

Rules are tried in order; the first whose regular expression matches the START of the part name wins. Target:
  "helm" | "chest" | "gloves" | "pants" | "boots"   the whole part
  None                                              the part is dropped (it lives elsewhere now, e.g. in the base body)
  ("region", {region: slot})                        the part is cut along the body-region borders (a mail shell that
                                                    covers the whole body, a sleeve that runs into a glove)
Waist rule: everything hung from the belt (skirts, tassets of cloth, charms, scabbards) stays with the chest piece; the
pants slot gets what is fitted to the legs.
"""

DEF_RS = {"head": "helm", "neck": "chest", "torso": "chest", "upper_arms": "chest", "forearms": "chest", "hands": "gloves",
          "hips": "pants", "thighs": "pants", "shins": "pants", "feet": "boots"}
ARM_RS = dict(DEF_RS, forearms="gloves")

T4MAP = {
    "lampknight": [
        (r"helm|coif", "helm"),
        (r"gloves|vambrace|cuff|couter", "gloves"),
        (r"trousers|poleyn", "pants"),
        (r"sabatons|greave", "boots"),
        (r".*", "chest"),
    ],
    "emberblade": [
        (r"helm|visor", "helm"),
        (r"gauntlets|vambrace|cuff|couter", "gloves"),
        (r"cuisse|poleyn|tasset", "pants"),
        (r"sabatons|greave", "boots"),
        (r"mail", ("region", ARM_RS)),
        (r".*", "chest"),
    ],
    "astromancer": [
        (r"boots", "boots"),
        (r"inner_skirt", "pants"),
        (r".*", "chest"),
    ],
    "hunter": [
        (r"hood|cowl_roll|mask", "helm"),
        (r"bracer|gloves", "gloves"),
        (r"trousers|kneecop", "pants"),
        (r"boots", "boots"),
        (r".*", "chest"),
    ],
    "soulbinder": [
        (r"hood|crown|antler|visor|eye_", "helm"),
        (r"wraps|cord_", "gloves"),
        (r"trousers", "pants"),
        (r"boots", "boots"),
        (r".*", "chest"),
    ],
    "wildsoul": [
        (r"wolf|hood_ruff", "helm"),
        (r"wraps|palm_wraps|cord_r|bark_bracer|rune_bracer|bracer_cord", "gloves"),
        (r"trousers", "pants"),
        (r"boots|boot_fur|garter", "boots"),
        (r".*", "chest"),
    ],
    "forgebody": [
        (r"headband", "helm"),
        (r"topknot", None),
        (r"wraps|palm_wraps|wrap_cord", "gloves"),
        (r"pant_|pants_seat", "pants"),
        (r"shoes|shin_wraps|shin_cord", "boots"),
        (r".*", "chest"),
    ],
    "pactbinder": [
        (r"circlet|prong", "helm"),
        (r"glove_r|arm_chain|shackle_wrist|brand_ring|brand_f3|brand_f4|brand_t4", "gloves"),
        (r"char_arm", ("region", ARM_RS)),
        (r"trousers", "pants"),
        (r"boots", "boots"),
        (r".*", "chest"),
    ],
}

# per piece options of the split: ray = reach of the coverage rays (m), never_hide / force_hide = region tokens,
# cinch = (z0, z1, gap): parts of this piece above z0 are drawn in to `gap` over the body and squashed below z1, so a
# skirt that used to hang from another piece's belt tucks under any chest hem
T4OPT = {
    ("lampknight", "pants"): dict(never_hide=("wrap_loin", "hips")),
    ("astromancer", "pants"): dict(cinch=(0.93, 1.03, 0.010), ray=0.09, never_hide=("wrap_loin", "hips", "thighs")),
    ("soulbinder", "helm"): dict(force_hide=("hair",)),
    ("wildsoul", "helm"): dict(force_hide=("hair",)),
    ("hunter", "helm"): dict(force_hide=("hair",)),
}
