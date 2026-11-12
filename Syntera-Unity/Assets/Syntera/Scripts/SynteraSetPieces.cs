using System.Collections.Generic;
using Syntera.Core;
using UnityEngine;

namespace Syntera
{
    // Procedural set dressing for each chapter location, plus world state that reflects the
    // player's record (riders or standby fleet, mayor's hologram, grid lights, sunset, council
    // seats). Placeholder primitives only: production environments are not supplied here.
    public sealed class SynteraSetPieces : MonoBehaviour
    {
        private readonly Dictionary<string, Vector3> markerOverrides = new Dictionary<string, Vector3>();
        private Transform root;
        private Transform reactive;
        private Light sun;
        private Color sunColor;
        private Quaternion sunRotation;
        private readonly List<Light> gridLights = new List<Light>();
        private readonly List<Renderer> billboards = new List<Renderer>();
        private TextMesh hologram;
        private readonly List<Renderer> seats = new List<Renderer>();
        private readonly List<TextMesh> seatLabels = new List<TextMesh>();
        private Font font;

        public Vector3 LeverPosition { get; private set; }
        public Vector3 NoahPosition { get; private set; }
        public IList<Vector3> RideRoute { get; private set; }

        public bool TryMarker(string objectiveId, out Vector3 position) => markerOverrides.TryGetValue(objectiveId, out position);

        public void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = new GameObject("Syntera set pieces (placeholder)").transform;
            sun = FindFirstObjectByType<Light>();
            if (sun != null) { sunColor = sun.color; sunRotation = sun.transform.rotation; }

            foreach (var place in SynteraWorld.Places) ClearTowers(Flat(place), 24);
            BuildStrike(Flat("Nexus Junction"));
            BuildCivic(Flat("Civic Hall"));
            BuildCeremony(Flat("Lumen Gardens"));
            BuildSubstation(Flat("Volta Substation"));
            BuildHousing(Flat("Haven Heights"));
            BuildLab(Flat("Quantum Hill"));
            BuildTram(Flat("Signal Square"));
            BuildRooftop(Flat("Loopline HQ"));
            BuildMarket(Flat("Circuit Market"));
            BuildCouncil(Flat("Civic Hall") + new Vector3(0, 0, 12));
            BuildRideRoute();
        }

        private static Vector3 Flat(string place)
        {
            var p = SynteraWorld.PlacePosition(place);
            return new Vector3(p.x, 0, p.z);
        }

        // Procedural towers are random; clear them from every set piece and the ride corridor.
        private static void ClearTowers(Vector3 center, float radius)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = r.gameObject.name;
                if (n != "Tower" && n != "Rooftop solar") continue;
                Vector3 d = r.transform.position - center; d.y = 0;
                if (d.magnitude < radius) Destroy(r.gameObject);
            }
        }

        private static void ClearCorridor(Vector3 a, Vector3 b, float halfWidth)
        {
            float len = Vector3.Distance(a, b);
            for (float s = 0; s <= len; s += halfWidth) ClearTowers(Vector3.Lerp(a, b, len < .01f ? 0 : s / len), halfWidth + 4);
        }

        // ------------------------------------------------------------- CH01 strike, bridge rescue
        private void BuildStrike(Vector3 p)
        {
            var bridgeBase = p + new Vector3(10, 0, 10);
            Box("Maintenance ramp", bridgeBase + new Vector3(-5.5f, 1.6f, 0), new Vector3(9, .4f, 3), new Color(.35f, .38f, .42f), Quaternion.Euler(0, 0, 20f));
            Box("Maintenance bridge", bridgeBase + new Vector3(5, 3.15f, 0), new Vector3(13, .4f, 3), new Color(.4f, .42f, .46f));
            Box("Bridge rail N", bridgeBase + new Vector3(5, 3.85f, 1.45f), new Vector3(13, 1, .1f), new Color(.9f, .55f, .1f));
            Box("Bridge rail S", bridgeBase + new Vector3(5, 3.85f, -1.45f), new Vector3(13, 1, .1f), new Color(.9f, .55f, .1f));
            LeverPosition = bridgeBase + new Vector3(10.5f, 3.9f, 0);
            NoahPosition = bridgeBase + new Vector3(8, 4.3f, 0);
            var lever = Box("Rescue lever", LeverPosition, new Vector3(.25f, 1.1f, .25f), new Color(.95f, .15f, .1f));
            lever.transform.rotation = Quaternion.Euler(0, 0, 25f);
            markerOverrides["ST01_OBJ_RESCUE"] = LeverPosition;
            Label("RESCUE LEVER", LeverPosition + Vector3.up * 1.2f, .25f);
            var rnd = new System.Random(2026);
            for (int i = 0; i < 24; i++)
            {
                var drone = Shape(PrimitiveType.Sphere, "Frozen delivery drone", p + new Vector3((float)rnd.NextDouble() * 40 - 20, 12 + (float)rnd.NextDouble() * 8, (float)rnd.NextDouble() * 40 - 20), Vector3.one * .9f, new Color(.85f, .85f, .9f));
                Destroy(drone.GetComponent<Collider>());
            }
            Label("DELIVERY ROBOTS DECLARE INDUSTRIAL ACTION", p + new Vector3(0, 9, 18), .6f);
            Label("LIFE WITHOUT DELAYS", p + new Vector3(-14, 7, 16), .5f);
            var depot = p + new Vector3(-12, 0, 6);
            Box("Kato's depot", depot + new Vector3(0, 2, 0), new Vector3(8, 4, 6), new Color(.55f, .45f, .35f));
            markerOverrides["ST01_OBJ_INSPECT"] = depot + new Vector3(0, 1.25f, -4.2f);
            BuildPip(depot + new Vector3(3, 0, -4.5f));
        }

        private void BuildPip(Vector3 at)
        {
            Box("Pip-04 body", at + new Vector3(0, .6f, 0), new Vector3(.8f, .9f, .8f), new Color(.9f, .9f, .95f));
            var head = Shape(PrimitiveType.Sphere, "Pip-04 head", at + new Vector3(0, 1.3f, 0), Vector3.one * .55f, new Color(.2f, .8f, .9f));
            Destroy(head.GetComponent<Collider>());
            Label("WE REQUEST A CACTUS", at + new Vector3(0, 2f, 0), .22f);
            var crate = Box("Crate", at + new Vector3(1.4f, .4f, 0), new Vector3(.8f, .8f, .8f), new Color(.6f, .45f, .25f));
            var cactus = Shape(PrimitiveType.Capsule, "Management (cactus)", crate.transform.position + new Vector3(0, .75f, 0), new Vector3(.3f, .35f, .3f), new Color(.2f, .6f, .25f));
            Destroy(cactus.GetComponent<Collider>());
            Label("MANAGEMENT", crate.transform.position + new Vector3(0, 1.5f, 0), .18f);
        }

        // ------------------------------------------------------------- CH03 mayor's hologram
        private void BuildCivic(Vector3 p)
        {
            Box("Civic Hall", p + new Vector3(-10, 4, 8), new Vector3(10, 8, 8), new Color(.75f, .72f, .65f));
            var holo = Shape(PrimitiveType.Capsule, "Mayor hologram", p + new Vector3(8, 15, 10), new Vector3(4, 15, 4), new Color(.3f, .8f, 1f, .35f));
            Destroy(holo.GetComponent<Collider>());
            hologram = Label("I RESIGN. I AM PURSUING JAZZ.", p + new Vector3(8, 31, 10), .9f);
            for (int i = 0; i < 3; i++)
                Box("Projection relay " + (i + 1), p + new Vector3(-4 + i * 4, 1, -6), new Vector3(.6f, 2, .6f), new Color(.2f, .9f, 1f));
        }

        // ------------------------------------------------------------- CH05 ride and ceremony
        private void BuildCeremony(Vector3 p)
        {
            Box("Pavilion roof", p + new Vector3(6, 4, 8), new Vector3(10, .3f, 8), new Color(.95f, .92f, .85f));
            for (int i = 0; i < 4; i++) Box("Pavilion post", p + new Vector3(1.5f + (i % 2) * 9, 2, 4.5f + (i / 2) * 7), new Vector3(.3f, 4, .3f), Color.white);
            for (int i = 0; i < 5; i++) Box("Gift basket", p + new Vector3(3 + i * 1.3f, .4f, 7), new Vector3(.8f, .6f, .8f), new Color(.75f, .55f, .25f));
            Box("Annual reports", p + new Vector3(5, .2f, 9), new Vector3(2, .3f, 1.2f), Color.white);
            for (int t = 0; t < 3; t++)
            {
                var tier = Shape(PrimitiveType.Cylinder, "Cake tier", p + new Vector3(9, .6f + t * .45f, 9), new Vector3(1.4f - t * .35f, .2f, 1.4f - t * .35f), new Color(1f, .95f, .9f));
                Destroy(tier.GetComponent<Collider>());
            }
            var drone = Shape(PrimitiveType.Sphere, "Ceremony cargo drone", p + new Vector3(8, 6, 8), Vector3.one * 1.4f, new Color(.6f, .6f, .65f));
            Destroy(drone.GetComponent<Collider>());
            Label("PROJECTED FIVE-YEAR RETURNS: 42%", p + new Vector3(8, 8, 8), .3f);
            markerOverrides["ST05_OBJ_SHUTDOWN"] = p + new Vector3(8, 1.25f, 5);
        }

        private void BuildRideRoute()
        {
            Vector3 start = new Vector3(-8, 0, -11), end = Flat("Lumen Gardens");
            // Along the boulevard at z=0, round the roundabout at x=-32, then north to the gardens.
            RideRoute = new List<Vector3> { start, new Vector3(-8, 0, 0), new Vector3(-32, 0, 0), new Vector3(-32, 0, 17), new Vector3(end.x, 0, 17) };
            for (int i = 1; i < RideRoute.Count; i++) ClearCorridor(RideRoute[i - 1], RideRoute[i], 6);
            Shape(PrimitiveType.Cylinder, "Obstacle roundabout", new Vector3(-32, .4f, 0), new Vector3(5, .4f, 5), new Color(.3f, .55f, .3f));
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(-14 - i * 5, .5f, (i % 2 == 0) ? 1.8f : -1.8f);
                Shape(PrimitiveType.Cylinder, "Obstacle roadworks cone", c, new Vector3(.5f, .5f, .5f), new Color(1f, .45f, .05f));
                Label("ROADWORKS", c + Vector3.up * 1.2f, .15f);
            }
            var goat = Box("Obstacle goat", new Vector3(-32, .6f, 10), new Vector3(.6f, .7f, 1.1f), new Color(.95f, .95f, .9f));
            goat.AddComponent<SynteraWanderer>().Configure(new Vector3(-34, .6f, 10), new Vector3(-30, .6f, 10), .8f);
            Label("GOAT", goat.transform.position + Vector3.up, .15f);
            for (int i = 0; i < 2; i++)
            {
                var ped = Shape(PrimitiveType.Capsule, "Obstacle pedestrian", new Vector3(-20 + i * 6, 1, -3), new Vector3(.7f, 1, .7f), new Color(.8f, .4f, .5f));
                ped.AddComponent<SynteraWanderer>().Configure(new Vector3(-20 + i * 6, 1, -3.5f), new Vector3(-20 + i * 6, 1, 3.5f), 1.2f);
            }
        }

        // ------------------------------------------------------------- CH06 grid
        private void BuildSubstation(Vector3 p)
        {
            for (int i = 0; i < 3; i++) Box("Substation", p + new Vector3(-8 + i * 5, 1.5f, 10), new Vector3(3, 3, 3), new Color(.45f, .5f, .55f));
            string[] sectors = { "Hospital", "Homes", "Industry", "Billboards" };
            for (int i = 0; i < sectors.Length; i++)
            {
                var pos = p + new Vector3(-12 + i * 8, 0, 20);
                Box(sectors[i] + " block", pos + new Vector3(0, 3, 0), new Vector3(5, 6, 5), new Color(.3f, .35f, .4f));
                var lo = new GameObject(sectors[i] + " light");
                lo.transform.SetParent(root);
                lo.transform.position = pos + new Vector3(0, 8, -3);
                var l = lo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 14; l.intensity = 0; l.color = new Color(1f, .85f, .5f);
                gridLights.Add(l);
                Label(sectors[i].ToUpper(), pos + new Vector3(0, 7, -2.6f), .3f);
            }
            for (int i = 0; i < 3; i++)
            {
                var bb = Box("Ad billboard", p + new Vector3(-20 + i * 20, 9, -20), new Vector3(8, 4, .3f), new Color(.95f, .3f, .7f));
                billboards.Add(bb.GetComponent<Renderer>());
            }
            Label("A SUNRISE, ADVERTISED DURING A BLACKOUT", p + new Vector3(0, 13, -20), .35f);
        }

        // Skyline reflects the allocation chosen on the grid console (intensity 0..1 per sector).
        public void ShowGridAllocation(IList<float> fractions)
        {
            for (int i = 0; i < gridLights.Count && i < fractions.Count; i++) gridLights[i].intensity = 3f * fractions[i];
        }

        // ------------------------------------------------------------- CH07-CH12 locations
        private void BuildHousing(Vector3 p)
        {
            for (int i = 0; i < 3; i++) Box("Haven Heights flats", p + new Vector3(-10 + i * 9, 6, 12), new Vector3(7, 12, 6), new Color(.8f, .78f, .72f));
            var bb = Box("Housing hologram", p + new Vector3(0, 9, -14), new Vector3(10, 4, .3f), new Color(.4f, .9f, 1f));
            billboards.Add(bb.GetComponent<Renderer>());
            Label("RENT YOU CAN AFFORD. JUST BE YOURSELF.", p + new Vector3(0, 11.6f, -14), .35f);
        }

        private void BuildLab(Vector3 p)
        {
            Box("Raven's forensics lab", p + new Vector3(0, 2.5f, 12), new Vector3(10, 5, 8), new Color(.25f, .28f, .35f));
            Box("Evidence desk", p + new Vector3(0, .5f, 6), new Vector3(3, 1, 1.2f), new Color(.5f, .5f, .55f));
            markerOverrides["ST08_OBJ_TIMESTAMPS"] = p + new Vector3(-1, 1.25f, 4.8f);
            markerOverrides["ST08_OBJ_ACCOUNT"] = p + new Vector3(1.5f, 1.25f, 4.8f);
        }

        private void BuildTram(Vector3 p)
        {
            Box("Lakeside tram rail", p + new Vector3(0, .05f, 8), new Vector3(60, .1f, 1.5f), new Color(.5f, .5f, .5f));
            Box("Lakeside tram", p + new Vector3(6, 1.6f, 8), new Vector3(12, 3, 2.6f), new Color(.85f, .2f, .2f));
            Box("Rally stage", p + new Vector3(-8, .4f, -8), new Vector3(8, .8f, 4), new Color(.3f, .3f, .35f));
            markerOverrides["ST09_OBJ_RALLY"] = p + new Vector3(-8, 1.25f, -5);
        }

        private void BuildRooftop(Vector3 p)
        {
            Box("Loopline HQ", p + new Vector3(10, 4, 10), new Vector3(10, 8, 10), new Color(.35f, .55f, .7f));
            Box("Rooftop ramp", p + new Vector3(10, 4, 1.2f), new Vector3(3, .4f, 17), new Color(.4f, .4f, .45f), Quaternion.Euler(-28f, 0, 0));
            markerOverrides["ST10_OBJ_ROOFTOP"] = p + new Vector3(10, 9.25f, 12);
            Label("LOOPLINE", p + new Vector3(10, 10, 4.9f), .5f);
        }

        private void BuildMarket(Vector3 p)
        {
            Box("Mama Sarah's stall", p + new Vector3(-6, 1, 6), new Vector3(4, 2, 2), new Color(.85f, .6f, .3f));
            Box("Stall awning", p + new Vector3(-6, 2.6f, 5.6f), new Vector3(4.4f, .15f, 2.8f), new Color(.2f, .55f, .8f));
            Label("PHARMACY · EGG-ROLLS", p + new Vector3(-6, 3.3f, 6), .25f);
        }

        private void BuildCouncil(Vector3 p)
        {
            Box("Council chamber floor", p + new Vector3(0, .05f, 8), new Vector3(16, .1f, 10), new Color(.5f, .45f, .4f));
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-60, 60, i / 8f) * Mathf.Deg2Rad;
                var seat = Box("Council seat " + (i + 1), p + new Vector3(Mathf.Sin(a) * 6, .6f, 8 + Mathf.Cos(a) * 4), new Vector3(1, 1.2f, 1), new Color(.5f, .5f, .5f));
                seats.Add(seat.GetComponent<Renderer>());
                seatLabels.Add(Label("", seat.transform.position + Vector3.up * 1.3f, .2f));
            }
            Label("DO NOT ACTIVATE", p + new Vector3(0, 2.5f, 3), .3f);
        }

        // Seat-by-seat vote: colour plus text (FOR / AGAINST) so it never relies on colour alone.
        public void ShowVote(CouncilResult vote)
        {
            int seat = 0;
            foreach (var bloc in vote.Votes)
                for (int s = 0; s < bloc.SeatsAvailable && seat < seats.Count; s++, seat++)
                {
                    bool yes = s < bloc.SeatsFor;
                    seats[seat].material.color = yes ? new Color(.2f, .8f, .3f) : new Color(.85f, .2f, .2f);
                    seatLabels[seat].text = (yes ? "FOR" : "AGAINST") + "\n" + bloc.BlocName;
                }
        }

        // ------------------------------------------------------------- world reacting to the record
        public void ApplyWorldState(CampaignDirector director)
        {
            var state = director.Engine.State;
            if (reactive != null) Destroy(reactive.gameObject);
            reactive = new GameObject("Reactive world state").transform;
            reactive.SetParent(root);
            string strike = state.ChoiceFor("ST01_STRIKE");
            var junction = SynteraWorld.PlacePosition("Nexus Junction");
            if (strike != null)
                for (int i = 0; i < 6; i++)
                {
                    bool riders = strike == "A";
                    var go = Shape(riders ? PrimitiveType.Capsule : PrimitiveType.Sphere, riders ? "Moto-taxi rider" : "Standby fleet drone",
                        junction + new Vector3(-12 + i * 4, riders ? 1 : 4, -12), riders ? new Vector3(.6f, .9f, .6f) : Vector3.one * .8f,
                        riders ? new Color(1f, .55f, .1f) : new Color(.15f, .15f, .2f), reactive);
                    Destroy(go.GetComponent<Collider>());
                    go.AddComponent<SynteraWanderer>().Configure(go.transform.position, go.transform.position + new Vector3(0, 0, 24), riders ? 2.5f : 4f);
                }
            string civic = state.ChoiceFor("ST03_CIVIC");
            if (hologram != null)
                hologram.text = civic == null ? "I RESIGN. I AM PURSUING JAZZ." : civic == "A" ? "THE MAYOR APOLOGISES. LOGS PUBLISHED." : "I HAVE NEVER PLAYED JAZZ.";
            string grid = state.ChoiceFor("ST06_BLACKOUT");
            bool blackout = director.Chapter.Id == "ST06" && grid == null;
            foreach (var b in billboards) if (b != null) b.enabled = !blackout && !(director.Chapter.Id == "ST09");
            if (grid != null && gridLights.Count == 4)
                ShowGridAllocation(grid == "A" ? new[] { 1f, .6f, .3f, 0f } : new[] { 1f, 1f, 1f, .5f });
            else if (!blackout) ShowGridAllocation(new[] { 1f, 1f, 1f, 1f });
            if (sun != null)
            {
                bool sunset = director.Chapter.Id == "ST09";
                sun.color = sunset ? new Color(1f, .55f, .3f) : sunColor;
                sun.transform.rotation = sunset ? Quaternion.Euler(8, -60, 0) : sunRotation;
            }
            foreach (var r in seats) if (state.CharterId == null) r.material.color = new Color(.5f, .5f, .5f);
            if (state.CharterId == null) foreach (var t in seatLabels) t.text = "";
            if (director.Vote != null) ShowVote(director.Vote);
        }

        // ------------------------------------------------------------- helpers
        private GameObject Box(string name, Vector3 pos, Vector3 scale, Color color, Quaternion? rot = null)
        {
            var g = Shape(PrimitiveType.Cube, name, pos, scale, color);
            if (rot.HasValue) g.transform.rotation = rot.Value;
            return g;
        }

        private GameObject Shape(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent != null ? parent : root);
            g.transform.position = pos;
            g.transform.localScale = scale;
            var m = SynteraMaterials.Make(color);
            g.GetComponent<Renderer>().material = m;
            return g;
        }

        private TextMesh Label(string text, Vector3 pos, float size)
        {
            var go = new GameObject("Label: " + text);
            go.transform.SetParent(root);
            go.transform.position = pos;
            go.AddComponent<SynteraBillboardLabel>();
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.characterSize = size;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            go.GetComponent<MeshRenderer>().material = font.material;
            return tm;
        }
    }

    // Back-and-forth mover for pedestrians, the goat, riders and drones.
    public sealed class SynteraWanderer : MonoBehaviour
    {
        private Vector3 a, b;
        private float speed;
        private bool toB = true;

        public void Configure(Vector3 from, Vector3 to, float metresPerSecond)
        {
            a = from; b = to; speed = metresPerSecond;
            transform.position = from;
        }

        private void Update()
        {
            Vector3 target = toB ? b : a;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude < .01f) toB = !toB;
        }
    }

    // Keeps world labels facing the camera so they stay readable.
    public sealed class SynteraBillboardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
