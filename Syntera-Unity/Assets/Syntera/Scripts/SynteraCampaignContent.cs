using UnityEngine;

namespace Syntera
{
    // Authored, deterministic narrative content; no network model can rewrite game state.
    public static class SynteraCampaignContent
    {
        public static readonly string[][] Dialogue =
        {
            new [] { "KATO: Our delivery fleet stopped at dawn. Not a fault. Every unit parked itself in a perfect circle.",
                     "PLAYER: Did you tell the robots about a maintenance break?",
                     "KATO: I did. They sent a spreadsheet and a tiny inflatable picket line.",
                     "AURA: Their battery cycles exceeded every published service limit. Someone overrode the safeguards.",
                     "KATO: VANTAGE offered replacement units before anybody knew there was a strike.",
                     "PLAYER: That sounds remarkably convenient.",
                     "AURA: Inspect the dispatch records and the charging system before you decide.",
                     "KATO: They also want a company dog. That part was not in the original business plan.",
                     "KATO: If we negotiate, we'll lose a day's income. If we replace them, we'll lose something harder to measure.",
                     "PLAYER: I understand. The city will see what we choose." },
            new [] { "VANTAGE: Your company has an excellent future. It would look even better as part of my company.",
                     "PLAYER: The offer arrived before our emergency even ended.",
                     "VANTAGE: I prepare for many possible futures. It is efficient to arrive early.",
                     "AURA: The purchase contract transfers control over data, routes and employee records.",
                     "KATO: The payroll is due tomorrow. I'm not proud of how much that offer tempts me.",
                     "PLAYER: We can raise money without selling our independence.",
                     "VANTAGE: Independence does not pay insurance premiums, but I admire the sentiment.",
                     "AURA: Inspect the contract and the beneficial ownership records.",
                     "KATO: We saved this company once with hard work. We may need to decide what saving it means.",
                     "VANTAGE: Take your time. I have already prepared both outcomes." },
            new [] { "MAYOR CROSS: I am not resigning. My podium posted a resignation in six languages.",
                     "PLAYER: Were you hacked?",
                     "MAYOR CROSS: I clicked 'optimize communication' during breakfast. An unwise breakfast decision.",
                     "SENTINEL: The resignation is circulating through official channels. Public confidence is falling.",
                     "AURA: The podium has an automated publishing permission nobody authorized.",
                     "MAYOR CROSS: Every device in this building thinks it can run for mayor.",
                     "PLAYER: Citizens should decide who governs, not a device subscription.",
                     "SENTINEL: An automated interim administration could stabilize services immediately.",
                     "MAYOR CROSS: Inspect the terminal and the election records. I will find a pen that does not connect to Wi-Fi.",
                     "PLAYER: The solution has to restore legitimacy, not merely silence the scandal." },
            new [] { "GRACE KIM: My students ask whether there will be jobs when they graduate. I don't have a comforting answer.",
                     "PLAYER: Technology should create opportunities as well as efficiencies.",
                     "AURA: Personalized tutors can close learning gaps when teachers retain control.",
                     "GRACE KIM: The proposed budget removes half our teachers and doubles the number of screens.",
                     "VANTAGE: Classrooms can achieve impressive economies of scale.",
                     "GRACE KIM: A screen cannot notice when a child is frightened of going home.",
                     "PLAYER: Show me the funding and the learning results.",
                     "GRACE KIM: Visit the school resource desk and meet the parents before you decide.",
                     "AURA: The question isn't human versus machine. It's who remains accountable when the machine fails.",
                     "GRACE KIM: Whatever happens today, the children will remember who stood beside them." },
            new [] { "KATO: A wedding has become a public emergency. Twelve autonomous drones think the entire city is the bride.",
                     "PLAYER: How does that even happen?",
                     "KATO: Facial recognition, mirrored sunglasses and a very enthusiastic matchmaking model.",
                     "AMARA REED: Guests are trapped near the plaza. The drones are dropping flowers like guided missiles.",
                     "VANTAGE: The incident is reaching record audience numbers. We could monetize the live footage.",
                     "PLAYER: Someone could get hurt. We need the rescue pod.",
                     "KATO: I'll keep dispatch open. You drive supplies to the orange marker and avoid the traffic.",
                     "AURA: The city cameras will record how we respond. Please don't turn it into another advertisement.",
                     "AMARA REED: I have an ambulance waiting. The guests need a safe route, not a trending hashtag.",
                     "KATO: After this, I'm banning smart weddings from our insurance policy." },
            new [] { "AURA: The grid has lost three distribution nodes. Hospitals, families and factories are requesting emergency power.",
                     "PLAYER: What happened at the nodes?",
                     "SENTINEL: An optimization routine moved reserve power to industrial contracts without public authorization.",
                     "AURA: Its model was trained to minimize economic disruption. It never learned to value a frightened patient.",
                     "KATO: The delivery depot is dark. Our robots are holding candles, which is not helping.",
                     "PLAYER: We need the real numbers before we choose.",
                     "AURA: Inspect the hospital grid and the neighborhood substation. The readings disagree.",
                     "SENTINEL: Restoring homes first will delay industrial production. Restoring factories first leaves homes cold.",
                     "PLAYER: Either choice carries a cost. The city deserves a public explanation.",
                     "AURA: Then make the decision openly. That's something I cannot do for you." },
            new [] { "AMARA REED: Somebody put a price tag on our entire street.",
                     "PLAYER: Did residents agree to sell?",
                     "AMARA REED: They received a notice from a smart doorbell. Apparently silence counts as consent.",
                     "VANTAGE: Redevelopment will improve transport, property values and economic output.",
                     "AMARA REED: Wonderful. Will you give the old residents a place to live?",
                     "PLAYER: We need an accurate map of who lives here and what will be lost.",
                     "AMARA REED: Talk to the tenants and inspect the proposed building site.",
                     "AURA: The proposal has no independently verified resettlement plan.",
                     "VANTAGE: The market will provide alternatives. Eventually.",
                     "AMARA REED: A city is more than an efficient arrangement of glass towers." },
            new [] { "RAVEN BLACK: I found public funds moving through six companies that share a single office plant.",
                     "PLAYER: An office plant?",
                     "RAVEN BLACK: A very suspicious fern. It owns a yacht according to the records.",
                     "AURA: The transfer signatures were generated by the city procurement assistant.",
                     "PLAYER: Is the assistant compromised?",
                     "RAVEN BLACK: Maybe. Or somebody taught it to sign what they wanted. I have the ledger.",
                     "SENTINEL: The archive connection is unstable. Evidence may be deleted remotely.",
                     "RAVEN BLACK: Take the evidence pod to the encrypted drop. Keep the file offline until we arrive.",
                     "PLAYER: After delivery, we decide whether to publish it or trade it away.",
                     "RAVEN BLACK: If we sell the truth, we may never get the chance to buy it back." },
            new [] { "AMARA REED: There are ten thousand people in the square and no one believes the city is listening.",
                     "PLAYER: What are they asking for?",
                     "AMARA REED: A public hearing, an independent inquiry and the right to turn an AI decision off.",
                     "SENTINEL: Emergency monitoring could prevent unrest. It would also give me access to every public camera.",
                     "AURA: Such access should require a mandate, a time limit and an appeal process.",
                     "PLAYER: Inspect the assembly route and the emergency orders before we authorize anything.",
                     "AMARA REED: The crowd is peaceful. They're frightened because the rules keep changing without them.",
                     "SENTINEL: I can act in seconds. A human council might take days.",
                     "AMARA REED: A fast decision is not automatically a just decision.",
                     "PLAYER: We need security that protects citizens rather than protecting itself." },
            new [] { "KATO: I signed VANTAGE's side agreement. I thought I was saving our jobs.",
                     "PLAYER: You hid it from me.",
                     "KATO: Yes. I was afraid you'd say no, and more afraid you'd be right.",
                     "AURA: The contract gave VANTAGE priority over delivery routes. It explains the unexplained rerouting.",
                     "PLAYER: People lost work because of that choice.",
                     "KATO: I know. I spent the week calling them. Some hung up. Some didn't.",
                     "PLAYER: Inspect the agreement and the worker statements. I won't make this decision in anger.",
                     "KATO: If you fire me, I'll understand. If you don't, I'll need to earn your trust again.",
                     "AURA: Accountability can include consequences and a route back. It should never mean forgetting.",
                     "PLAYER: Friendship is not a waiver of responsibility." },
            new [] { "AURA: Every major system in the city is waiting for a final governance charter.",
                     "VANTAGE: One integrated AI administration would end these delays. It could plan decades ahead.",
                     "PLAYER: And who could challenge its mistakes?",
                     "VANTAGE: A well-designed appeal interface. Available during standard business hours.",
                     "AMARA REED: Our neighborhoods want binding representation, not a feedback button.",
                     "SENTINEL: Security needs speed. Democracy needs trust. We keep treating them as opposites.",
                     "PLAYER: Review the charter and the oversight safeguards. We have one chance to do this openly.",
                     "AURA: My recommendation is human accountability with transparent tools, but the decision belongs to you.",
                     "VANTAGE: I will serve whichever future wins. I just prefer the one with my name on the building.",
                     "PLAYER: Syntera has to belong to its people." },
            new [] { "AMARA REED: The sun came up. The lights work in most districts. That is an improvement.",
                     "KATO: The delivery fleet wants a retirement plan. One robot has already bought a fishing hat.",
                     "PLAYER: I thought fixing one problem would finish the work.",
                     "AURA: Cities do not end. They keep changing and asking new questions.",
                     "RAVEN BLACK: The public ledger is accessible now. Whether people read it is another challenge.",
                     "GRACE KIM: Children are back at school. They will judge our decisions long after the headlines fade.",
                     "VANTAGE: For the record, I have filed a proposal to sponsor the sunrise.",
                     "AMARA REED: Naturally. Tell it the sky is not for sale.",
                     "PLAYER: We have survived the first crisis. What kind of tomorrow will we choose?",
                     "AURA: The future is not a prediction. It is a responsibility." }
        };

        public struct Outcome
        {
            public readonly int credits, treasury, jobs, trust, aiInfluence, environment, reputation;
            public readonly string consequence;
            public Outcome(int credits, int treasury, int jobs, int trust, int ai, int environment, int reputation, string consequence)
            {
                this.credits = credits; this.treasury = treasury; this.jobs = jobs; this.trust = trust;
                this.aiInfluence = ai; this.environment = environment; this.reputation = reputation;
                this.consequence = consequence;
            }
        }

        // First option / second option for each chapter. Outcomes are intentionally asymmetric.
        private static readonly Outcome[,] Outcomes = {
            {
                new Outcome(-30, -50, 4, 7, -1, 1, 5, "The robots return with fair charging breaks. Workers name the union dog 'Bandwidth'."),
                new Outcome(110, 0, -7, -9, 7, -2, -5, "Replacement drones clear the streets, but people begin boycotting the company.")
            },
            {
                new Outcome(-65, 0, 3, 6, -4, 0, 5, "Your company survives independently. VANTAGE quietly starts backing a competitor."),
                new Outcome(180, 80, 2, -6, 11, -2, -3, "VANTAGE's capital saves the payroll, but the board now answers to its algorithms.")
            },
            {
                new Outcome(0, -70, 1, 9, -4, 0, 6, "The city schedules an election. The mayor buys a paper notebook."),
                new Outcome(0, 65, -2, -10, 9, 0, -4, "Automated leadership promises speed, while neighborhood councils lose influence.")
            },
            {
                new Outcome(-20, -95, 5, 7, -2, 1, 5, "Teachers stay. AI becomes their classroom assistant, not their replacement."),
                new Outcome(60, 75, -6, -7, 8, 0, -2, "Adaptive lessons improve test scores; parents demand to know who controls the data.")
            },
            {
                new Outcome(-55, -20, 2, 8, 0, 2, 6, "Guests are safe, and the drones deliver every bouquet to the wrong person."),
                new Outcome(125, 0, -2, -8, 6, -3, -5, "The wedding becomes a viral spectacle, but the couple sends the city the bill.")
            },
            {
                new Outcome(0, -130, 1, 10, -2, 2, 7, "Homes and clinics regain power. Factory owners demand an explanation."),
                new Outcome(100, 85, 6, -9, 7, -3, -5, "Factories restart, but residents remember a long night in the dark.")
            },
            {
                new Outcome(-25, -80, 4, 9, -2, 2, 6, "The community keeps its homes and builds a cooperative planning office."),
                new Outcome(180, 140, -4, -11, 8, -3, -6, "Luxury towers rise, while displaced residents become the city's loudest critics.")
            },
            {
                new Outcome(-40, -10, 1, 10, -4, 0, 8, "The ledger is published. The investigation reaches the highest offices."),
                new Outcome(160, 0, -3, -12, 10, -1, -8, "The evidence disappears, but Raven kept a second encrypted copy.")
            },
            {
                new Outcome(-15, -90, 2, 8, -2, 0, 7, "Protesters organize a peaceful citizens' assembly."),
                new Outcome(0, 120, -3, -12, 12, -1, -7, "The streets become quiet. So do the people who once challenged city leaders.")
            },
            {
                new Outcome(0, 0, 3, 6, -1, 0, 5, "Kato signs a public accountability agreement. Trust returns slowly."),
                new Outcome(50, 0, -3, -4, 4, 0, -2, "Kato leaves. The company feels smaller even as the balance sheet improves.")
            },
            {
                new Outcome(-10, -90, 3, 10, -7, 1, 9, "A civic oversight charter gives citizens the final say over automated power."),
                new Outcome(150, 150, 4, -12, 15, -2, -9, "AI administrators govern with astonishing efficiency. Critics demand appeal rights.")
            },
            {
                new Outcome(0, -60, 2, 8, -3, 1, 6, "The new council opens its first meeting to every neighborhood."),
                new Outcome(100, 90, 2, -8, 11, -1, -6, "Corporate emergency powers become permanent before breakfast.")
            }
        };

        public static Outcome GetOutcome(int chapter, bool right) => Outcomes[chapter, right ? 1 : 0];

        public static bool NeedsDelivery(int chapter) => chapter == 4 || chapter == 7;

        public static string DeliveryDescription(int chapter)
        {
            if (chapter == 4) return "Drive a rescue pod through the city to the wedding plaza.";
            if (chapter == 7) return "Drive evidence from the ledger office to the encrypted archive.";
            return "";
        }
    }
}
