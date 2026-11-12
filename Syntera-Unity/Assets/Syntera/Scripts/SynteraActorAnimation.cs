using UnityEngine;

namespace Syntera
{
    // Procedural NPC idle/gesture fallback until licensed humanoid animation assets are imported.
    public sealed class SynteraActorAnimation : MonoBehaviour
    {
        private Transform leftArm;
        private Transform rightArm;
        private Transform head;
        private Vector3 origin;
        private float variation;
        private Transform mouth;
        private bool speaking;
        private float speakingLevel;

        private void Start()
        {
            origin = transform.position;
            variation = (Mathf.Abs(gameObject.name.GetHashCode() % 1000) / 1000f) * 6f;
            leftArm = Limb("Left arm",new Vector3(-.74f, .02f,0),new Vector3(.24f,.70f,.26f));
            rightArm = Limb("Right arm",new Vector3(.74f,.02f,0),new Vector3(.24f,.70f,.26f));
            head = Limb("Head",new Vector3(0,.80f,0),new Vector3(.65f,.52f,.57f));
            mouth = Limb("Mouth",new Vector3(0,.48f,-.32f),new Vector3(.26f,.07f,.06f));
        }

        private Transform Limb(string label, Vector3 offset, Vector3 size)
        {
            GameObject part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=label;
            part.transform.SetParent(transform,false);
            part.transform.localPosition=offset;
            // Parent capsule is scaled by the city builder; compensate for inherited scale.
            Vector3 parent=transform.lossyScale;
            part.transform.localScale=new Vector3(size.x/Mathf.Max(parent.x,.01f),
                size.y/Mathf.Max(parent.y,.01f),size.z/Mathf.Max(parent.z,.01f));
            var collider=part.GetComponent<Collider>();
            if(collider!=null) Destroy(collider);
            var renderer=part.GetComponent<Renderer>();
            renderer.material=new Material(Shader.Find("Standard"));
            renderer.material.color=new Color(.32f,.48f,.66f);
            return part.transform;
        }

        private void Update()
        {
            float phase=Time.time*.8f+variation;
            transform.position=origin+Vector3.up*(Mathf.Sin(phase*1.6f)*.03f);
            if(leftArm!=null) leftArm.localRotation=Quaternion.Euler(Mathf.Sin(phase)*12f,0,-12f);
            if(rightArm!=null) rightArm.localRotation=Quaternion.Euler(-Mathf.Sin(phase)*12f,0,12f);
            if(head!=null) head.localRotation=Quaternion.Euler(0,Mathf.Sin(phase*.45f)*10f,0);
            if(mouth!=null)
            {
                Vector3 size = mouth.localScale;
                size.y = (.07f + (speaking ? (.13f + speakingLevel * .22f) * (.5f + .5f * Mathf.Abs(Mathf.Sin(phase * 9f))) : 0f)) / Mathf.Max(transform.lossyScale.y,.01f);
                mouth.localScale = size;
            }
        }

        public void SetSpeaking(bool active, float level = .35f)
        {
            speaking = active;
            speakingLevel = Mathf.Clamp01(level);
        }

        public void SetHomePosition(Vector3 position)
        {
            origin=position;
            transform.position=position;
        }
    }
}
