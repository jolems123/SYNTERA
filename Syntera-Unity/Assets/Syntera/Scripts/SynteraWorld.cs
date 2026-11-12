using UnityEngine;

namespace Syntera
{
    public static class SynteraWorld
    {
        private static readonly string[] Names = {"Nexus Central", "Horizon Commons", "Quantum Valley", "Nova Heights", "Helix Medical", "Forge Industrial", "Eden Gardens", "The Underground"};
        private static readonly string[] People = {"Amara Reed", "Kato Mercer", "AURA", "VANTAGE", "Mayor Adrian Cross", "Grace Kim", "Raven Black", "Dr. Elias Morgan"};
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
        public static void Build()
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
            for (int i = 0; i < People.Length; i++)
            {
                float x = (i % 4 - 1.5f)*19, z = (i / 4 - .5f)*24;
                var actor = Shape(PrimitiveType.Capsule, "Character - " + People[i], new Vector3(x,1.15f,z), new Vector3(.9f,1.15f,.9f),i%2==0?new Color(.85f,.65f,.39f):new Color(.25f,.45f,.87f),root);
                actor.AddComponent<SynteraCharacter>().characterName = People[i];
                actor.AddComponent<SynteraActorAnimation>();
                SynteraCharacterAssets.Attach(actor, People[i], false);
            }
            for (int i = 0; i < Names.Length; i++)
            {
                float x = (i%4-1.5f)*32,z=(i/4-.5f)*34;
                Shape(PrimitiveType.Cylinder, "District beacon - "+ Names[i],new Vector3(x,2,z),new Vector3(.55f,2,.55f),new Color(.12f,.89f,.8f),root);
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
