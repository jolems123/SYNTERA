using System.Collections.Generic;
using UnityEngine;

namespace Syntera
{
    // Procedural placeholder city (primitive shapes, not production art).
    public static class SynteraWorld
    {
        // Screenplay v1.1 locations with the project's modern place names.
        public static readonly string[] Places =
        {
            "Nexus Junction", "Loopline HQ", "Civic Hall", "Signal Square", "Synapse Commons",
            "Lumen Gardens", "Volta Substation", "Haven Heights", "Quantum Hill", "Circuit Market"
        };

        public static Vector3 PlacePosition(int index)
        {
            return new Vector3((index % 5 - 2) * 32f, 1.3f, (index / 5 - .5f) * 34f);
        }

        public static Vector3 PlacePosition(string place)
        {
            int i = System.Array.IndexOf(Places, place);
            return i < 0 ? Vector3.zero : PlacePosition(i);
        }

        private static Material Material(Color c)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            return m;
        }

        private static GameObject Shape(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.position = position;
            g.transform.localScale = scale;
            g.transform.SetParent(parent);
            g.GetComponent<Renderer>().material = Material(color);
            return g;
        }

        public static void Build(IList<string> people)
        {
            var root = new GameObject("Syntera City - Procedural Scene").transform;
            Shape(PrimitiveType.Cube, "City ground", new Vector3(0, -.65f, 0), new Vector3(185, 1, 170), new Color(.18f,.26f,.29f), root);
            for (int x = -2; x <= 2; x++)
                Shape(PrimitiveType.Cube, "Road avenue", new Vector3(x * 32, -.07f, 0), new Vector3(7, .08f, 160), new Color(.10f,.13f,.18f), root);
            for (int z = -2; z <= 2; z++)
                Shape(PrimitiveType.Cube, "Road boulevard", new Vector3(0, -.06f, z * 32), new Vector3(175, .08f, 7), new Color(.1f,.13f,.18f), root);
            var rand = new System.Random(2050);
            for (int x = -5; x <= 5; x++)
            for (int z = -5; z <= 5; z++)
            {
                float px = x * 14, pz = z * 13;
                if (Mathf.Abs((px + 16) % 32) < 6 || Mathf.Abs((pz + 16) % 32) < 6) continue;
                float h = 7 + (float)rand.NextDouble() * 37;
                Color glass = Color.Lerp(new Color(.21f,.38f,.49f),new Color(.38f,.55f,.63f),(float)rand.NextDouble());
                Shape(PrimitiveType.Cube, "Tower", new Vector3(px,h/2,pz), new Vector3(8,h,8),glass,root);
                Shape(PrimitiveType.Cube, "Rooftop solar", new Vector3(px,h+.13f,pz),new Vector3(7,.18f,7),new Color(.07f,.16f,.35f),root);
            }
            for (int i = 0; i < people.Count; i++)
            {
                float x = (i % 5 - 2f) * 15f + 4f, z = (i / 5 - 1f) * 20f + 4f;
                var actor = Shape(PrimitiveType.Capsule, "Character - " + people[i], new Vector3(x,1.15f,z), new Vector3(.9f,1.15f,.9f),i%2==0?new Color(.85f,.65f,.39f):new Color(.25f,.45f,.87f),root);
                actor.AddComponent<SynteraCharacter>().characterName = people[i];
                actor.AddComponent<SynteraActorAnimation>();
                SynteraCharacterAssets.Attach(actor, people[i], false);
            }
            for (int i = 0; i < Places.Length; i++)
            {
                Vector3 p = PlacePosition(i);
                Shape(PrimitiveType.Cylinder, "Place beacon - " + Places[i], new Vector3(p.x, 2, p.z), new Vector3(.55f,2,.55f), new Color(.12f,.89f,.8f), root);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = (i%4-1.5f)*32,z=(i/4-1.5f)*32;
                Shape(PrimitiveType.Sphere,"Tree crown",new Vector3(x+6,4,z+6),new Vector3(4,5,4),new Color(.14f,.42f,.29f),root);
                Shape(PrimitiveType.Cylinder,"Tree trunk",new Vector3(x+6,1.5f,z+6),new Vector3(.4f,1.5f,.4f),new Color(.31f,.23f,.16f),root);
            }
            for (int i = 0; i < 8; i++)
            {
                var car=Shape(PrimitiveType.Cube,"Autonomous vehicle "+i,new Vector3(-60+i*15,.7f,2),new Vector3(3,1.2f,1.8f),new Color(.1f,.68f,.85f),root);
                car.AddComponent<SynteraTraffic>().speed=4+i*.4f;
            }
        }
    }

    public sealed class SynteraCharacter : MonoBehaviour
    {
        public string characterName;
    }

    public sealed class SynteraTraffic : MonoBehaviour
    {
        public float speed = 5;
        private void Update()
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime, Space.World);
            if (transform.position.x > 78) transform.position = new Vector3(-78,transform.position.y,transform.position.z);
        }
    }
}
