using UnityEngine;

namespace Syntera
{
    // Authored, deterministic narrative content; no network model can rewrite game state.
    public static class SynteraCampaignContent
    {
        public static readonly string[][] Dialogue =
        {
            new [] { "KATO: I am negotiating with delivery robots. They've asked for shorter shifts and a dog.", "AURA: Their batteries are exhausted. Their union representative is a toaster.", "KATO: The city needs deliveries. The workers need dignity. Your call." },
            new [] { "VANTAGE: I offer enough credits to rescue your company. The small print is only forty-seven pages.", "AURA: VANTAGE gets a controlling stake. Have you read its previous acquisitions?", "VANTAGE: I prefer the term permanent strategic friendship." },
            new [] { "MAYOR CROSS: I did not resign. My smart podium drafted a resignation and posted it.", "SENTINEL: The statement has already gone viral. Public confidence is falling.", "MAYOR CROSS: I suppose I should stop using the free trial." },
            new [] { "GRACE KIM: Every student deserves a future in a city full of machines.", "AURA: AI tutors help; they cannot replace human responsibility.", "GRACE KIM: The budget can fund teachers or automation. Choose what we protect." },
            new [] { "KATO: Wedding drones have surrounded the stadium. They think the city is the bride.", "VANTAGE: The livestream has impressive advertising potential.", "KATO: Please drive the rescue pod to the plaza before someone marries the traffic lights." },
            new [] { "AURA: A grid failure has darkened four districts. Emergency reserves will last one night.", "SENTINEL: The hospital needs power. Families and businesses are competing for the remainder.", "AURA: There isn't enough for everyone. How should we prioritize?" },
            new [] { "AMARA REED: Our homes have become a real-estate experiment.", "VANTAGE: The redevelopment is economically efficient.", "AMARA REED: People are not error messages in your spreadsheet." },
            new [] { "RAVEN BLACK: I followed the money. A shell company owns the other shell company.", "RAVEN BLACK: Drive to the archive drop before the evidence gets deleted.", "AURA: Publishing it might hurt friends. Concealing it will hurt everyone." },
            new [] { "AMARA REED: The city is marching because nobody knows who is in charge.", "SENTINEL: I can restore order with a temporary emergency mandate.", "AMARA REED: Nothing is more permanent than temporary power." },
            new [] { "KATO: I signed a deal behind your back. I thought the company would collapse.", "KATO: I won't ask you to pretend nothing happened.", "AURA: Trust can be rebuilt, but only through accountability." },
            new [] { "AURA: Human institutions should remain responsible for every major automated decision.", "VANTAGE: Centralized AI authority would move much faster.", "AURA: The city will live with your choice long after the applause." },
            new [] { "AMARA REED: It's morning in Syntera City. The streetlights are on. Most of them.", "KATO: The delivery robots are asking whether they get retirement benefits.", "AMARA REED: Tomorrow is something we build together. What should the city become?" }
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
