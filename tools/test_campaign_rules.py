"""Independent reference simulation of syntera_campaign_rules.json.

Reimplements the rule interpreter separately from the C# engine so the two can
cross-check each other. Both must reproduce screenplay v1.1 section 14.
Run: python tools/test_campaign_rules.py
"""
import itertools
import json
import pathlib
import unittest

RULES_PATH = pathlib.Path(__file__).resolve().parents[1] / "Syntera-Unity/Assets/Syntera/Resources/syntera_campaign_rules.json"


def load_rules():
    return json.loads(RULES_PATH.read_text(encoding="utf-8"))


class Sim:
    def __init__(self, rules):
        self.r = rules
        self.stats = dict(rules["start"]["stats"])
        self.owner = dict(rules["start"]["ownership"])
        self.flags = set()
        self.lim = rules["limits"]

    def bound(self, stat, v):
        if stat in self.lim["clampedStats"]:
            return max(self.lim["clampMin"], min(self.lim["clampMax"], v))
        if stat in self.lim["floorZeroStats"]:
            return max(0, v)
        return v

    def add(self, stat, delta):
        self.stats[stat] = self.bound(stat, self.stats[stat] + delta)

    def evidence(self):
        return sum(1 for c in self.r["evidenceCards"] if self.any_met(c["whenAny"]))

    def read(self, stat):
        return self.evidence() if stat == "evidence" else self.stats[stat]

    def cond(self, c):
        if "flag" in c:
            return (c["flag"] in self.flags) == c["is"]
        v = self.read(c["stat"])
        return ("min" not in c or v >= c["min"]) and ("max" not in c or v <= c["max"])

    def all_met(self, group):
        return all(self.cond(c) for c in group)

    def any_met(self, groups):
        return any(self.all_met(g) for g in groups)

    @staticmethod
    def cost(option, stat):
        return -sum(e["delta"] for e in option["effects"] if e["stat"] == stat and e["delta"] < 0)

    def affordable(self, option):
        return self.stats["credits"] >= self.cost(option, "credits") and self.stats["treasury"] >= self.cost(option, "treasury")

    def credit_line_used(self):
        return any(f in self.flags for f in self.r["creditLine"]["setFlags"])

    def apply(self, option):
        for e in option["effects"]:
            self.add(e["stat"], e["delta"])
        self.flags.update(option.get("setFlags", []))
        t = option.get("ownershipTransfer")
        if t:
            moved = min(t["percent"], self.owner.get(t["from"], 0))
            self.owner[t["from"]] -= moved
            self.owner[t["to"]] = self.owner.get(t["to"], 0) + moved
        for cf in option.get("conditionalFlags", []):
            if self.all_met(cf["whenAll"]):
                self.flags.add(cf["flag"])

    def year(self, y):
        for rule in y["rules"]:
            if self.all_met(rule.get("whenAll", [])):
                self.add(rule["stat"], rule["delta"])

    def vote(self, charter_id):
        council = self.r["council"]
        charter = next(c for c in council["charters"] if c["id"] == charter_id)
        seats = 0
        for bloc in council["blocs"]:
            for tier in charter["votes"][bloc["id"]]:
                if self.any_met(tier["whenAny"]):
                    seats += tier["seats"]
                    break
        if seats >= council["passSeats"]:
            return seats, True, charter["ending"]
        ending = next(f["ending"] for f in council["fallbacks"] if self.any_met(f["whenAny"]))
        return seats, False, ending


def run_intent(rules, intent):
    sim = Sim(rules)
    decisions = {d["id"]: d for d in rules["decisions"]}
    years = {y["id"]: y for y in rules["yearTransitions"]}
    actual, all_aff, neg, i = [], True, False, 0
    cl = rules["creditLine"]
    for step in rules["sequence"]:
        if step in years:
            sim.year(years[step])
        elif step in decisions:
            d = decisions[step]
            opts = {o["id"]: o for o in d["options"]}
            want = intent[i]
            i += 1
            o = opts[want]
            short_c = sim.stats["credits"] < sim.cost(o, "credits")
            short_t = sim.stats["treasury"] < sim.cost(o, "treasury")
            if short_c and not short_t and not sim.credit_line_used():
                sim.add("credits", cl["amount"])
                for e in cl["effects"]:
                    sim.add(e["stat"], e["delta"])
                sim.flags.update(cl["setFlags"])
            chosen = want
            if not sim.affordable(o):
                all_aff = False
                chosen = next(x["id"] for x in d["options"] if x["id"] != want and sim.affordable(x))
            sim.apply(opts[chosen])
            actual.append(chosen)
        neg = neg or sim.stats["credits"] < 0 or sim.stats["treasury"] < 0
    return sim, "".join(actual), all_aff, neg


class Section14(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rules = load_rules()
        cls.charters = [c["id"] for c in cls.rules["council"]["charters"]]
        cls.results = {}
        for intent in itertools.product("AB", repeat=len(cls.rules["decisions"])):
            intent = "".join(intent)
            cls.results[intent] = run_intent(cls.rules, intent)

    def test_path_count(self):
        self.assertEqual(1024, len(self.results))

    def test_charter_passes(self):
        passes = {c: 0 for c in self.charters}
        none = 0
        for sim, *_ in self.results.values():
            any_pass = False
            for c in self.charters:
                if sim.vote(c)[1]:
                    passes[c] += 1
                    any_pass = True
            none += not any_pass
        self.assertEqual({"shared": 242, "compact": 466, "covenant": 348}, passes)
        self.assertEqual(280, none)

    def test_affordability_and_credit_line(self):
        self.assertEqual(922, sum(r[2] for r in self.results.values()))
        self.assertEqual(32, sum(r[0].credit_line_used() for r in self.results.values()))
        self.assertEqual(0, sum(r[3] for r in self.results.values()))
        self.assertEqual(9, max(r[1].count("A") for r in self.results.values()))

    def test_extremes(self):
        sim, actual, *_ = self.results["A" * 10]
        self.assertEqual("AAAAAABAAB", actual)
        self.assertEqual([(5, True, "SharedDawn"), (3, False, "HumanFrontier"), (2, False, "HumanFrontier")],
                         [sim.vote(c) for c in self.charters])
        sim, actual, *_ = self.results["B" * 10]
        self.assertEqual("BBBBBBBBBB", actual)
        self.assertEqual([(3, False, "CorporateSunrise"), (5, True, "CorporateSunrise"), (2, False, "CorporateSunrise")],
                         [sim.vote(c) for c in self.charters])


DIALOGUE_PATH = RULES_PATH.with_name("syntera_campaign_dialogue.json")


def iter_lines(doc):
    for ch in doc["chapters"]:
        for line in ch["dialogue"]:
            yield ch["id"], line
        for group in ("optionDialogue", "charterDialogue"):
            for lines in ch.get(group, {}).values():
                for line in lines:
                    yield ch["id"], line
        for ending in ch.get("endings", {}).values():
            for line in ending["dialogue"]:
                yield ch["id"], line
    for beat in doc["conditionalBeats"]:
        for line in beat["variants"]:
            yield beat["chapter"], line


class Dialogue(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rules = load_rules()
        cls.doc = json.loads(DIALOGUE_PATH.read_text(encoding="utf-8"))
        cls.lines = list(iter_lines(cls.doc))

    def test_ids_unique_well_formed_and_filed(self):
        import re
        pattern = re.compile(r"^ST(0[1-9]|1[0-2])_([A-Z0-9]+)_[0-9]{3}$")
        ids = [line["id"] for _, line in self.lines]
        self.assertEqual(len(ids), len(set(ids)), "duplicate line IDs")
        self.assertEqual(210, len(ids))
        for chapter_id, line in self.lines:
            m = pattern.match(line["id"])
            self.assertIsNotNone(m, line["id"])
            self.assertTrue(line["id"].startswith(chapter_id + "_"), line["id"])
            self.assertEqual(m.group(2), line["speaker"], line["id"])
            self.assertIn(line["speaker"], self.doc["speakers"], line["id"])
            self.assertTrue(line["text"].strip(), line["id"])

    def test_references_match_rules(self):
        decisions = {d["id"]: d for d in self.rules["decisions"]}
        flags = {f for d in decisions.values() for o in d["options"] for f in o.get("setFlags", [])}
        flags |= {cf["flag"] for d in decisions.values() for o in d["options"] for cf in o.get("conditionalFlags", [])}
        flags |= set(self.rules["creditLine"]["setFlags"])
        covered = set()
        for ch in self.doc["chapters"]:
            if ch["decision"]:
                covered.add(ch["decision"])
                options = {o["id"] for o in decisions[ch["decision"]]["options"]}
                self.assertEqual(options, set(ch["optionDialogue"]), ch["id"])
        self.assertEqual(set(decisions), covered)
        for _, line in self.lines:
            for cond in line.get("whenAll", []):
                self.assertIn(cond["flag"], flags, line["id"])


if __name__ == "__main__":
    unittest.main()
