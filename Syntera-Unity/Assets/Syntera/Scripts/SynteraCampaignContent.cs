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

        public static bool NeedsDelivery(int chapter) => chapter == 4 || chapter == 7;

        public static string DeliveryDescription(int chapter)
        {
            if (chapter == 4) return "Drive a rescue pod through the city to the wedding plaza.";
            if (chapter == 7) return "Drive evidence from the ledger office to the encrypted archive.";
            return "";
        }
    }
}
